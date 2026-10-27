using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed record CollectionPreview(StudentRecord Student, CourseChoice Choice, ImportOrigin Origin,
    Guid? ExpectedActive, ValidationResult Validation, bool Duplicate, string Conflict);

public sealed class TeacherSession(IAdministrationStore store, OfferCatalog catalog, string actor)
{
    public OfferCatalog Catalog { get; } = catalog;
    public ImmutableArray<TeacherState> Overview() => [.. store.Students().Select(record =>
    {
        var active = store.History(record.Student.ExternalStudentId).LastOrDefault();
        var review = active is null ? null : Validate(active.Snapshot, record);
        var followup = store.Reselection(record.Student.ExternalStudentId);
        return new TeacherState(record, active, review, followup.Required || review?.Status == ReviewStatus.Invalid, followup.Deadline, followup.Note);
    })];
    private ValidationResult Validate(CourseChoice choice, StudentRecord record) => new ChoiceValidator().Validate(choice, Catalog,
        new ProfileContext(ProfileStatus.Verified, record.Student.Prerequisites));
    public CollectionPreview Preview(CourseChoice choice, string studentId, ImportOrigin origin)
    {
        var record = store.Students().Single(s => s.Student.ExternalStudentId == studentId);
        if (!record.MappingConfirmed
            || (!string.IsNullOrWhiteSpace(record.WindowsUsername) && !string.IsNullOrWhiteSpace(origin.AuthenticatedAccount) && !string.Equals(origin.AuthenticatedAccount, record.WindowsUsername, StringComparison.OrdinalIgnoreCase))
            || choice.StudentId != studentId || choice.GraduationYear != record.Student.GraduationYear)
            throw new InvalidOperationException("Herkunft, bestätigte Ordnerzuordnung oder Jahrgang stimmt nicht überein.");
        var history = store.History(studentId);
        var active = history.LastOrDefault();
        var duplicate = history.Any(r => r.ImportOrigin?.ContentHash == origin.ContentHash && r.ImportOrigin?.SourceId == origin.SourceId);
        var prior = history.LastOrDefault(r => r.Source == RevisionSource.StudentSubmission);
        var conflict = active is null || duplicate ? "" : active.Source != RevisionSource.StudentSubmission
            ? "Lehrerentscheidung vorhanden. Nur nach bewusster Konfliktentscheidung übernehmen."
            : prior is not null && (choice.DocumentId != prior.Snapshot.DocumentId || choice.Revision <= prior.Snapshot.Revision)
                ? "Abweichende Dokument-ID oder alte/gleiche Schülerrevision. Konfliktentscheidung erforderlich." : "";
        return new(record, choice, origin, active?.Id, Validate(choice, record), duplicate, conflict);
    }
    public void Confirm(CollectionPreview preview, bool confirmed, bool resolveConflict, string reason)
    {
        if (!confirmed) throw new InvalidOperationException("Import benötigt bestätigte Vorschau.");
        if (store.Students().Single(s => s.Student.ExternalStudentId == preview.Choice.StudentId).Version != preview.Student.Version)
            throw new InvalidOperationException("Stammdaten geändert; Vorschau erneuern.");
        if (store.History(preview.Choice.StudentId).LastOrDefault()?.Id != preview.ExpectedActive)
            throw new InvalidOperationException("Wahl geändert; Vorschau erneuern.");
        if (preview.Duplicate) return;
        if (preview.Conflict.Length > 0 && !resolveConflict) throw new InvalidOperationException(preview.Conflict);
        Save(preview.Choice, preview.ExpectedActive, RevisionSource.StudentSubmission, reason, preview.Origin);
    }
    public ChoiceRevision Save(CourseChoice choice, Guid? expectedActive, RevisionSource source, string reason, ImportOrigin? origin = null)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bearbeiter und Änderungsgrund erforderlich.");
        var record = store.Students().Single(s => s.Student.ExternalStudentId == choice.StudentId);
        if (record.Student.GraduationYear != choice.GraduationYear) throw new ArgumentException("Abiturjahrgang stimmt nicht überein.");
        var before = store.History(choice.StudentId).LastOrDefault();
        var changes = (before?.Snapshot.Selections ?? []).Select(s => s.OfferId).Union(choice.Selections.Select(s => s.OfferId))
            .Select(id => new SelectionChange(id, [.. (before?.Snapshot.Selections.SingleOrDefault(s => s.OfferId == id)?.Semesters ?? []).Order()], [.. (choice.Selections.SingleOrDefault(s => s.OfferId == id)?.Semesters ?? []).Order()]))
            .Where(c => !c.Before.SequenceEqual(c.After)).ToImmutableArray();
        var role = source == RevisionSource.StudentSubmission ? ActorRole.Student : ActorRole.Teacher;
        var revision = new ChoiceRevision(Guid.NewGuid(), expectedActive, choice.StudentId, DateTimeOffset.UtcNow, actor,
            role, source, reason, Catalog.Version, ChoiceValidator.Version, choice, changes, origin);
        _ = Validate(choice, record); // Always evaluate against current protected prerequisites, never a supplied profile.
        store.Append(revision, expectedActive);
        return revision;
    }
    public ChoiceRevision Restore(string studentId, Guid revisionId, Guid expectedActive, string reason)
    {
        var previous = store.History(studentId).Single(r => r.Id == revisionId);
        return Save(previous.Snapshot with { OfferVersion = Catalog.Version, RuleVersion = ChoiceValidator.Version, UpdatedAt = DateTimeOffset.UtcNow }, expectedActive, RevisionSource.Restoration, reason);
    }
    public void FollowUp(string studentId, bool required, DateTimeOffset? deadline, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Begründung erforderlich.");
        var state = Overview().Single(s => s.Record.Student.ExternalStudentId == studentId);
        if (!required && (state.Active is null || state.Validation?.Status != ReviewStatus.Valid))
            throw new InvalidOperationException("Abschluss erst nach gültiger Prüfung; offene Fachfreigaben bleiben needs_review.");
        store.SetReselection(studentId, required, deadline, actor + ": " + reason);
    }
}
