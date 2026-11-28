using System.Collections.Immutable;
using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed class ChoiceReconciliation(IAdministrationStore store, OfferCatalog catalog, string actor)
{
    public ReconciliationPreview Preview(ImmutableArray<ExternalChoice> external, ImportOrigin origin) =>
        Preview(external, origin, []);

    public ReconciliationPreview Preview(ImmutableArray<ExternalChoice> external, ImportOrigin origin,
        ImmutableArray<(int RowNumber, string DisplayName, string ClassName)> missingIdentities)
    {
        var students = store.Students().Where(s => s.Student.GraduationYear == catalog.GraduationYear).ToArray();
        var items = new List<ReconciliationItem>();
        foreach (var id in students.Select(s => s.Student.ExternalStudentId).Union(external.Select(e => e.StudentId), StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            var records = students.Where(s => string.Equals(s.Student.ExternalStudentId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            var candidates = external.Where(e => string.Equals(e.StudentId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (records.Length > 1 || candidates.Length > 1 || records.Any(s => s.Student.ExternalStudentId != id) || candidates.Any(e => e.StudentId != id))
            { items.Add(new(id, ReconciliationKind.InvalidIdentity, null, null, null, null, "Mehrdeutige ID; keine Zuordnung über Namen.")); continue; }
            var record = records.SingleOrDefault(); var candidate = candidates.SingleOrDefault();
            var current = record is null ? null : store.History(id).LastOrDefault();
            if (candidate is not null && candidate.GraduationYear != catalog.GraduationYear) throw new ArgumentException("Abweichender Jahrgang.");

            if (record is null && candidate is not null)
            {
                var info = string.Join(" · ", new[] { candidate.DisplayName, candidate.ClassName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var detail = info.Length > 0
                    ? $"Nur in ASV-Datei enthalten ({info})."
                    : "Neuzugang: zuerst Stammdaten mit Vorschau importieren und Voraussetzungen vervollständigen; danach Abgleich erneuern.";
                items.Add(new(id, ReconciliationKind.OnlyExternal, null, null, candidate, null, detail));
                continue;
            }

            if (record is not null && candidate is null)
            {
                var detail = record.Student.Departure switch
                {
                    DepartureStatus.ConfirmedDeparture => "In ASV-Datei nicht enthalten (in GSM-Kurswahl als bestätigter Abgänger markiert).",
                    DepartureStatus.ExpectedDeparture => "In ASV-Datei nicht enthalten (in GSM-Kurswahl als voraussichtlicher Abgänger markiert).",
                    _ => "In ASV-Datei nicht enthalten. Keine Löschung oder Änderung."
                };
                items.Add(new(id, ReconciliationKind.OnlyInternal, record, current, null, null, detail));
                continue;
            }

            var validation = record is not null && candidate is not null ? new ChoiceValidator().Validate(Candidate(candidate, current), catalog,
                new(ProfileStatus.Verified, record.Student.Prerequisites)) : null;

            if (record is not null && candidate is not null && (candidate.CompareOralSubjects || candidate.IgnoreBilingualVariant || !candidate.Warnings.IsEmpty))
            {
                var (differences, notes) = CompareDetailed(current, candidate);
                var kind = differences.Length == 0 ? ReconciliationKind.Identical : ReconciliationKind.Changed;
                var detail = kind == ReconciliationKind.Identical
                    ? (notes.Length == 0 ? "Kurswahl und mündliche Planung stimmen vollständig überein." : "Deckungsgleich (mit Hinweis zu bilingualem Kurs).")
                    : string.Join(" · ", differences);
                items.Add(new(id, kind, record, current, candidate, validation, detail)
                {
                    Differences = differences,
                    Notes = notes
                });
                continue;
            }

            var genericKind = current is not null && Same(current.Snapshot.Selections, candidate!.Selections)
                ? ReconciliationKind.Identical
                : ReconciliationKind.Changed;
            var genericDetail = genericKind == ReconciliationKind.Identical
                ? "Belegungen identisch. Keine neue Revision."
                : "Externe Belegungen ersetzen nach Einzelbestätigung die vollständige interne Belegung. Mündliche Planung bleibt erhalten; keine verbindliche Abgabe.";
            items.Add(new(id, genericKind, record, current, candidate, validation, genericDetail));
        }

        foreach (var missing in missingIdentities.OrderBy(m => m.RowNumber))
        {
            var detail = $"ASV-ID fehlt in Zeile {missing.RowNumber}"
                + (string.IsNullOrWhiteSpace(missing.DisplayName) ? "" : $" ({missing.DisplayName.Trim()})")
                + "; keine automatische Zuordnung über Namen.";
            items.Add(new($"(Zeile {missing.RowNumber})", ReconciliationKind.InvalidIdentity, null, null, null, null, detail)
            {
                Differences = [detail]
            });
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

    private (ImmutableArray<string> Differences, ImmutableArray<string> Notes) CompareDetailed(ChoiceRevision? current, ExternalChoice candidate)
    {
        var differences = new List<string>();
        var notes = new List<string>();

        if (current is null)
        {
            differences.Add(candidate.Selections.IsEmpty && candidate.PlannedOralSubjects.IsEmpty && candidate.Warnings.IsEmpty
                ? "Intern liegt noch keine Kurswahl vor (in ASV ebenfalls leer)."
                : "Intern liegt noch keine Kurswahl vor; in ASV sind Belegungen eingetragen.");
            differences.AddRange(candidate.Warnings);
            return ([.. differences], []);
        }

        (string SubjectId, CourseType Type, CourseVariant Variant) Key(CourseSelection s) =>
            candidate.IgnoreBilingualVariant ? (s.SubjectId, s.Type, CourseVariant.Normal) : (s.SubjectId, s.Type, s.Variant);

        var intGroups = current.Snapshot.Selections.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var extGroups = candidate.Selections.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var allKeys = intGroups.Keys.Union(extGroups.Keys)
            .OrderBy(k => SubjectName(k.SubjectId), StringComparer.CurrentCulture)
            .ThenBy(k => k.Type)
            .ThenBy(k => k.Variant)
            .ToArray();

        var onlyInternal = new List<CourseSelection>();
        var onlyExternal = new List<CourseSelection>();

        foreach (var key in allKeys)
        {
            var hasInt = intGroups.TryGetValue(key, out var intList);
            var hasExt = extGroups.TryGetValue(key, out var extList);

            if (hasInt && intList!.Length > 1)
                differences.Add($"{SubjectName(key.SubjectId)} {CourseTypeLabel(key.Type)}: intern mehrfach belegt.");
            if (hasExt && extList!.Length > 1)
                differences.Add($"{SubjectName(key.SubjectId)} {CourseTypeLabel(key.Type)}: in ASV mehrfach belegt.");
            if ((hasInt && intList!.Length > 1) || (hasExt && extList!.Length > 1))
                continue;

            if (hasInt && hasExt)
            {
                var intSel = intList![0];
                var extSel = extList![0];
                var intSem = intSel.Semesters.Order().ToArray();
                var extSem = extSel.Semesters.Order().ToArray();
                if (!intSem.SequenceEqual(extSem))
                    differences.Add($"{OfferLabel(intSel)}: intern {FormatSemesters(intSem)} ↔ ASV {FormatSemesters(extSem)}");
                else if (candidate.IgnoreBilingualVariant && intSel.Variant == CourseVariant.Bilingual)
                    notes.Add($"{OfferLabel(intSel)}: im ASV-Export nicht von der regulären Variante unterscheidbar; Fach, Kursart und Halbjahre stimmen überein.");
            }
            else if (hasInt)
                onlyInternal.Add(intList![0]);
            else if (hasExt)
                onlyExternal.Add(extList![0]);
        }

        var remainingSubjects = onlyInternal.Select(s => s.SubjectId).Union(onlyExternal.Select(s => s.SubjectId))
            .OrderBy(SubjectName, StringComparer.CurrentCulture)
            .ToArray();
        foreach (var subj in remainingSubjects)
        {
            var intSub = onlyInternal.Where(s => s.SubjectId == subj).ToArray();
            var extSub = onlyExternal.Where(s => s.SubjectId == subj).ToArray();
            if (intSub.Length == 1 && extSub.Length == 1)
            {
                differences.Add($"{SubjectName(subj)}: intern {CourseLabel(intSub[0])} ({FormatSemesters(intSub[0].Semesters)}) ↔ ASV {CourseLabel(extSub[0])} ({FormatSemesters(extSub[0].Semesters)})");
                continue;
            }
            foreach (var intSel in intSub)
                differences.Add($"Nur in GSM-Kurswahl: {OfferLabel(intSel)} ({FormatSemesters(intSel.Semesters)})");
            foreach (var extSel in extSub)
                differences.Add($"Nur in ASV: {OfferLabel(extSel)} ({FormatSemesters(extSel.Semesters)})");
        }

        differences.AddRange(candidate.Warnings);

        if (candidate.CompareOralSubjects)
        {
            var intOral = current.Snapshot.PlannedOralSubjects
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var extOral = candidate.PlannedOralSubjects
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!intOral.SequenceEqual(extOral, StringComparer.Ordinal))
                differences.Add($"Mündliche Prüfungsfächer: intern {FormatOral(current.Snapshot.PlannedOralSubjects)} ↔ ASV {FormatOral(candidate.PlannedOralSubjects)}");
        }

        return ([.. differences], [.. notes]);
    }

    private string SubjectName(string id) => catalog.Subjects.FirstOrDefault(s => s.Id == id)?.Name ?? id;
    private static string CourseTypeLabel(CourseType type) => type switch
    {
        CourseType.Lf => "LF",
        CourseType.Bf => "BF",
        CourseType.Elective => "Wahlfach",
        _ => "Seminarkurs"
    };
    private static string CourseLabel(CourseSelection s) => CourseTypeLabel(s.Type) + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "");
    private string OfferLabel(CourseSelection s) => $"{SubjectName(s.SubjectId)} {CourseLabel(s)}";
    private static string FormatSemesters(IEnumerable<int> semesters)
    {
        var ordered = semesters.Order().ToArray();
        return ordered.Length == 0 ? "keine" : "HJ " + string.Join(",", ordered);
    }
    private string FormatOral(IEnumerable<string> subjectIds)
    {
        var names = subjectIds
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SubjectName)
            .ToArray();
        return names.Length == 0 ? "keine" : string.Join(", ", names);
    }

    private CourseChoice Candidate(ExternalChoice external, ChoiceRevision? current)
    {
        var now = DateTimeOffset.UtcNow;
        var basis = current?.Snapshot ?? new CourseChoice(2, Guid.NewGuid(), external.StudentId, external.GraduationYear,
            catalog.Version, ChoiceValidator.Version, 1, now, now, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
        return basis with { Selections = [.. external.Selections.Select(s => external.IgnoreBilingualVariant && basis.Selections.FirstOrDefault(b => b.SubjectId == s.SubjectId && b.Type == s.Type && b.Variant == CourseVariant.Bilingual) is { } bilingual
                ? bilingual with { Semesters = s.Semesters } : s)],
            PlannedOralSubjects = external.CompareOralSubjects ? external.PlannedOralSubjects : basis.PlannedOralSubjects, OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version,
            UpdatedAt = now, SubmissionStatus = SubmissionStatus.Draft };
    }
    private static bool Same(ImmutableArray<CourseSelection> a, ImmutableArray<CourseSelection> b) =>
        JsonSerializer.Serialize(a.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s with { Semesters = [.. s.Semesters.Order()] }), DocumentJson.Options)
        == JsonSerializer.Serialize(b.OrderBy(s => s.OfferId, StringComparer.Ordinal).Select(s => s with { Semesters = [.. s.Semesters.Order()] }), DocumentJson.Options);
}
