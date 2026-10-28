using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed partial class SqliteAdministrationStore
{
    public bool PlanningEnabled => Convert.ToInt32(Scalar("PRAGMA user_version")) == 2;
    public void EnablePlanning()
    {
        if (PlanningEnabled) return;
        // Only called by the explicit activation button. The existing rows remain unchanged.
        using var tx = db.BeginTransaction();
        Execute("CREATE TABLE planning_snapshots (seq INTEGER PRIMARY KEY, id TEXT NOT NULL UNIQUE, body TEXT NOT NULL); CREATE TABLE planning_decisions (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);");
        foreach (var table in new[] { "planning_snapshots", "planning_decisions" })
            Execute($"CREATE TRIGGER {table}_no_update BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT,'append-only'); END; CREATE TRIGGER {table}_no_delete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT,'append-only'); END;");
        Execute("PRAGMA user_version=2"); tx.Commit();
    }
    private void RequirePlanning() { if (!PlanningEnabled) throw new InvalidOperationException("Kursplanung zuerst ausdrücklich aktivieren (Datenbankschema 2)."); }
    private static void ValidatePlanning(PlanningDocument document)
    {
        if (document.SchemaVersion != 1 || document.Id == Guid.Empty || document.CreatedAt == default || string.IsNullOrWhiteSpace(document.Name)
            || string.IsNullOrWhiteSpace(document.Actor) || string.IsNullOrWhiteSpace(document.RuleVersion)
            || document.Students.IsDefault || document.Choices.IsDefault || document.FollowUps.IsDefault || document.TargetGroupSize is < 1 or > 100)
            throw new InvalidDataException("Unbekanntes oder ungültiges Planungsschema.");
        ModelGuard.Catalog(document.Catalog);
        var ids = document.Students.Select(s => s.Student.ExternalStudentId).ToArray();
        if (ids.Distinct().Count() != ids.Length || document.Students.Any(s => s.SchemaVersion != 1 || s.Student.GraduationYear != document.Catalog.GraduationYear)
            || document.Choices.Select(c => c.StudentId).Distinct().Count() != document.Choices.Length
            || document.FollowUps.Select(f => f.StudentId).Distinct().Count() != ids.Length || document.FollowUps.Any(f => !ids.Contains(f.StudentId)))
            throw new InvalidDataException("Planungsbestand inkonsistent.");
        foreach (var c in document.Choices)
        {
            ModelGuard.Choice(c.Snapshot);
            if (!ids.Contains(c.StudentId) || c.StudentId != c.Snapshot.StudentId || c.Snapshot.GraduationYear != document.Catalog.GraduationYear)
                throw new InvalidDataException("Planungswahl inkonsistent.");
        }
    }
    public ImmutableArray<PlanningDocument> PlanningDocuments()
    {
        RequirePlanning(); var documents = Read<PlanningDocument>("SELECT body FROM planning_snapshots ORDER BY seq");
        foreach (var d in documents) ValidatePlanning(d);
        return documents;
    }
    public void SavePlanningDocument(PlanningDocument document)
    {
        RequirePlanning(); ValidatePlanning(document);
        Execute("INSERT INTO planning_snapshots(id,body) VALUES($p0,$p1)", document.Id.ToString(), Json(document));
    }
    private sealed record PlanningDecision(int SchemaVersion, Guid Id, DateTimeOffset At, string Actor, string Reason, Guid BasisId, OfferCatalog Catalog);
    public OfferCatalog? ActivePlanningCatalog()
    {
        if (!PlanningEnabled) return null;
        var decision = Read<PlanningDecision>("SELECT body FROM planning_decisions ORDER BY seq DESC LIMIT 1").FirstOrDefault();
        if (decision is null) return null;
        if (decision.SchemaVersion != 1) throw new InvalidDataException("Unbekanntes Planungsschema.");
        ModelGuard.Catalog(decision.Catalog); return decision.Catalog;
    }
    public DateTimeOffset? PlanningCatalogChangedAt() => PlanningEnabled
        ? Read<PlanningDecision>("SELECT body FROM planning_decisions ORDER BY seq DESC LIMIT 1").FirstOrDefault()?.At : null;
    public void CommitCancellation(CancellationPreview preview, string actor, string reason,
        ImmutableArray<ChoiceRevision> revisions, ImmutableArray<PlanningFollowUp> followUps)
    {
        RequirePlanning(); ValidatePlanning(preview.Basis); ModelGuard.Catalog(preview.Catalog);
        if (preview.Basis.IsScenario || string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason) || revisions.Length != preview.Basis.Choices.Length
            || preview.Catalog.GraduationYear != preview.Basis.Catalog.GraduationYear || preview.Catalog.Version == preview.Basis.Catalog.Version)
            throw new ArgumentException("Ungültige Planungsbestätigung.");
        using var tx = db.BeginTransaction();
        var currentCatalog = ActivePlanningCatalog();
        if (currentCatalog is not null && Json(currentCatalog) != Json(preview.Basis.Catalog)) throw new InvalidOperationException("Angebot geändert; Vorschau erneuern.");
        var students = Students().Where(s => s.Student.GraduationYear == preview.Catalog.GraduationYear).ToArray();
        if (Json(students) != Json(preview.Basis.Students)) throw new InvalidOperationException("Stammdaten geändert; Vorschau erneuern.");
        var currentChoices = students.Select(s => History(s.Student.ExternalStudentId).LastOrDefault()).OfType<ChoiceRevision>().ToArray();
        if (!currentChoices.Select(c => c.Id).SequenceEqual(preview.Basis.Choices.Select(c => c.Id))) throw new InvalidOperationException("Wahlen geändert; Vorschau erneuern.");
        foreach (var old in preview.Basis.FollowUps)
        {
            var current = Reselection(old.StudentId);
            if (current != (old.Required, old.Deadline, old.Note)) throw new InvalidOperationException("Nachwahl geändert; Vorschau erneuern.");
        }
        if (!PlanningDocuments().Any(d => d.Id == preview.Basis.Id)) SavePlanningDocument(preview.Basis);
        foreach (var revision in revisions)
        {
            ModelGuard.Choice(revision.Snapshot);
            if (revision.PreviousRevisionId != currentChoices.Single(c => c.StudentId == revision.StudentId).Id
                || revision.Source != (preview.Restoring ? RevisionSource.Restoration : RevisionSource.OfferCancellation))
                throw new InvalidOperationException("Ungültige Planungsrevision.");
            Execute("INSERT INTO revisions(id,student,body) VALUES($p0,$p1,$p2)", revision.Id.ToString(), revision.StudentId, Json(revision));
        }
        foreach (var f in followUps) SetReselection(f.StudentId, f.Required, f.Deadline, f.Note);
        Execute("INSERT INTO planning_decisions(body) VALUES($p0)", Json(new PlanningDecision(1, Guid.NewGuid(), DateTimeOffset.UtcNow, actor, reason, preview.Basis.Id, preview.Catalog)));
        tx.Commit();
    }
}
