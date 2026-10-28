using System.Collections.Immutable;
using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed class ChoiceReconciliation(IAdministrationStore store, OfferCatalog catalog, string actor)
{
    public ReconciliationPreview Preview(ImmutableArray<ExternalChoice> external, ImportOrigin origin)
    {
        var students = store.Students().Where(s => s.Student.GraduationYear == catalog.GraduationYear).ToArray();
        var items = new List<ReconciliationItem>();
        foreach (var id in students.Select(s => s.Student.ExternalStudentId).Union(external.Select(e => e.StudentId)).Order(StringComparer.Ordinal))
        {
            var records = students.Where(s => string.Equals(s.Student.ExternalStudentId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            var candidates = external.Where(e => string.Equals(e.StudentId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (records.Length > 1 || candidates.Length > 1 || records.Any(s => s.Student.ExternalStudentId != id) || candidates.Any(e => e.StudentId != id))
            { items.Add(new(id, ReconciliationKind.InvalidIdentity, null, null, null, null, "Mehrdeutige ID; keine Zuordnung über Namen.")); continue; }
            var record = records.SingleOrDefault(); var candidate = candidates.SingleOrDefault();
            var current = record is null ? null : store.History(id).LastOrDefault();
            if (candidate is not null && candidate.GraduationYear != catalog.GraduationYear) throw new ArgumentException("Abweichender Jahrgang.");
            var kind = record is null ? ReconciliationKind.OnlyExternal : candidate is null ? ReconciliationKind.OnlyInternal
                : current is not null && Same(current.Snapshot.Selections, candidate.Selections) ? ReconciliationKind.Identical : ReconciliationKind.Changed;
            var validation = record is not null && candidate is not null ? new ChoiceValidator().Validate(Candidate(candidate, current), catalog,
                new(ProfileStatus.Verified, record.Student.Prerequisites)) : null;
            items.Add(new(id, kind, record, current, candidate, validation, kind switch {
                ReconciliationKind.OnlyExternal => "Neuzugang: zuerst Stammdaten mit Vorschau importieren und Voraussetzungen vervollständigen; danach Abgleich erneuern.",
                ReconciliationKind.OnlyInternal => "In externer Datei nicht enthalten. Keine Löschung oder Änderung.",
                ReconciliationKind.Identical => "Belegungen identisch. Keine neue Revision.",
                _ => "Externe Belegungen ersetzen nach Einzelbestätigung die vollständige interne Belegung. Mündliche Planung bleibt erhalten; keine verbindliche Abgabe." }));
        }
        return new(origin, catalog.Version, [.. items]);
    }

    public ChoiceRevision Confirm(ReconciliationPreview preview, string studentId, bool confirmed, string reason)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Bestätigung und Änderungsgrund erforderlich.");
        if (preview.CatalogVersion != catalog.Version) throw new InvalidOperationException("Angebot geändert; Vorschau erneuern.");
        var item = preview.Items.Single(i => i.StudentId == studentId);
        if (item.Kind != ReconciliationKind.Changed || item.Student is null || item.External is null) throw new InvalidOperationException("Keine übernehmbare Abweichung.");
        var record = store.Students().Single(s => s.Student.ExternalStudentId == studentId);
        if (record.Version != item.Student.Version || store.History(studentId).LastOrDefault()?.Id != item.Internal?.Id)
            throw new InvalidOperationException("Stammdaten oder Wahl geändert; Vorschau erneuern.");
        return new TeacherSession(store, catalog, actor).Save(Candidate(item.External, item.Internal), item.Internal?.Id,
            RevisionSource.AsvReconciliation, reason, preview.Origin);
    }

    private CourseChoice Candidate(ExternalChoice external, ChoiceRevision? current)
    {
        var now = DateTimeOffset.UtcNow;
        var basis = current?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), external.StudentId, external.GraduationYear,
            catalog.Version, ChoiceValidator.Version, 1, now, now, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        return basis with { Selections = external.Selections, OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version,
            UpdatedAt = now, SubmissionStatus = SubmissionStatus.Draft };
    }
    private static bool Same(ImmutableArray<CourseSelection> a, ImmutableArray<CourseSelection> b) =>
        JsonSerializer.Serialize(a.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s with { Semesters = [.. s.Semesters.Order()] }), DocumentJson.Options)
        == JsonSerializer.Serialize(b.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s with { Semesters = [.. s.Semesters.Order()] }), DocumentJson.Options);
}
