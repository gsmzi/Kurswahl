using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public static class CoursePlanning
{
    public static PlanningDocument Capture(IAdministrationStore store, OfferCatalog catalog, string actor, string name, bool includeExpected)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Name und Bearbeiter erforderlich.");
        var students = store.Students().Where(s => s.Student.GraduationYear == catalog.GraduationYear).ToImmutableArray();
        return new(1, Guid.NewGuid(), DateTimeOffset.UtcNow, name.Trim(), actor, ChoiceValidator.Version, catalog, students,
            [.. students.Select(s => store.History(s.Student.ExternalStudentId).LastOrDefault()).OfType<ChoiceRevision>()],
            [.. students.Select(s => { var f = store.Reselection(s.Student.ExternalStudentId); return new PlanningFollowUp(s.Student.ExternalStudentId, f.Required, f.Deadline, f.Note); })], includeExpected);
    }

    private static bool Relevant(StudentRecord s, bool includeExpected) => s.Student.Departure == DepartureStatus.Active
        || includeExpected && s.Student.Departure == DepartureStatus.ExpectedDeparture;

    public static PlanningAnalysis Analyze(PlanningDocument snapshot, int? groupSize = null)
    {
        var targetGroupSize = groupSize ?? snapshot.TargetGroupSize;
        if (targetGroupSize is < 1 or > 100) throw new ArgumentException("Planungsgruppengröße muss zwischen 1 und 100 liegen.");
        var students = snapshot.Students.ToDictionary(s => s.Student.ExternalStudentId);
        var choices = snapshot.Choices.ToDictionary(c => c.StudentId, c => c.Snapshot);
        (int All, int Departures, int Relevant) Count(IEnumerable<string> ids)
        {
            var rows = ids.Distinct().Select(id => students[id]).ToArray();
            return (rows.Length, rows.Count(s => s.Student.Departure != DepartureStatus.Active), rows.Count(s => Relevant(s, snapshot.IncludeExpectedDepartures)));
        }
        var demand = ImmutableArray.CreateBuilder<DemandRow>();
        foreach (var offer in snapshot.Catalog.Offers.OrderBy(o => o.Id, StringComparer.Ordinal))
        foreach (var semester in offer.Schedule.OrderBy(s => s.Semester))
        {
            var count = Count(choices.Where(c => c.Value.Selections.Any(s => s.OfferId == offer.Id && s.Semesters.Contains(semester.Semester))).Select(c => c.Key));
            demand.Add(new(offer.Id, semester.Semester, count.All, count.Departures, count.Relevant, (int)Math.Ceiling((double)count.Relevant / targetGroupSize)));
        }
        var combinations = choices.GroupBy(c => string.Join(" + ", c.Value.Selections.Where(s => s.Type == CourseType.Lf).Select(s => s.OfferId).Order(StringComparer.Ordinal)))
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => { var c = Count(g.Select(x => x.Key)); return new CombinationRow(g.Key.Length == 0 ? "Ohne LF" : g.Key, c.All, c.Departures, c.Relevant); }).ToImmutableArray();
        var overlaps = ImmutableArray.CreateBuilder<OverlapRow>();
        var offers = snapshot.Catalog.Offers.Select(o => o.Id).Order(StringComparer.Ordinal).ToArray();
        for (var i = 0; i < offers.Length; i++)
        for (var j = i + 1; j < offers.Length; j++)
        for (var semester = 1; semester <= 4; semester++)
        {
            var count = Count(choices.Where(c => c.Value.Selections.Any(s => s.OfferId == offers[i] && s.Semesters.Contains(semester))
                && c.Value.Selections.Any(s => s.OfferId == offers[j] && s.Semesters.Contains(semester))).Select(c => c.Key));
            if (count.All > 0) overlaps.Add(new(offers[i], offers[j], semester, count.All, count.Departures, count.Relevant));
        }
        return new(students.Count, students.Values.Count(s => s.Student.Departure == DepartureStatus.ExpectedDeparture),
            students.Values.Count(s => s.Student.Departure == DepartureStatus.ConfirmedDeparture), students.Values.Count(s => Relevant(s, snapshot.IncludeExpectedDepartures)),
            students.Keys.Count(id => !choices.ContainsKey(id)), demand.ToImmutable(), combinations, overlaps.ToImmutable());
    }

    public static CancellationPreview PreviewCancellation(PlanningDocument basis, IEnumerable<string> cancelledIds)
    {
        var ids = cancelledIds.Distinct().Order(StringComparer.Ordinal).ToImmutableArray();
        if (ids.IsEmpty || ids.Any(id => !basis.Catalog.Offers.Any(o => o.Id == id && o.Status is OfferStatus.Offered or OfferStatus.Established)))
            throw new ArgumentException("Mindestens ein aktuell angebotenes Angebot zur Streichung wählen.");
        var catalog = basis.Catalog with { Version = "plan-" + Guid.NewGuid().ToString("N"), Offers = [.. basis.Catalog.Offers.Select(o => ids.Contains(o.Id) ? o with { Status = OfferStatus.Cancelled } : o)] };
        return BuildPreview(basis, catalog, ids, false);
    }

    public static CancellationPreview PreviewOfferRestoration(PlanningDocument basis, PlanningDocument previous)
    {
        if (basis.Catalog.GraduationYear != previous.Catalog.GraduationYear
            || !basis.Catalog.Offers.Select(o => o.Id).Order().SequenceEqual(previous.Catalog.Offers.Select(o => o.Id).Order()))
            throw new ArgumentException("Planungsstände enthalten unterschiedliche Jahrgänge oder Angebotskennungen.");
        var catalog = previous.Catalog with { Version = "plan-" + Guid.NewGuid().ToString("N") };
        var cancelled = catalog.Offers.Where(o => o.Status is not (OfferStatus.Offered or OfferStatus.Established)).Select(o => o.Id).ToImmutableArray();
        return BuildPreview(basis, catalog, cancelled, true);
    }

    private static CancellationPreview BuildPreview(PlanningDocument basis, OfferCatalog catalog, ImmutableArray<string> ids, bool restoring)
    {
        var changes = basis.Choices.Select(revision =>
        {
            var student = basis.Students.Single(s => s.Student.ExternalStudentId == revision.StudentId);
            var choice = revision.Snapshot with { OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version,
                Revision = revision.Snapshot.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow,
                Selections = [.. revision.Snapshot.Selections.Where(s => !ids.Contains(s.OfferId))] };
            var profile = new ProfileContext(ProfileStatus.Verified, student.Student.Prerequisites);
            var validation = new ChoiceValidator().Validate(choice, catalog, profile);
            var removed = revision.Snapshot.Selections.Where(s => ids.Contains(s.OfferId)).Select(s => s.OfferId).ToImmutableArray();
            // Suggestions are only personally permitted offers, never an automatic replacement or a promise of a legal complete choice.
            var replacements = catalog.Offers.Where(o => o.Status is OfferStatus.Offered or OfferStatus.Established
                && !choice.Selections.Any(s => s.SubjectId == o.SubjectId)
                && new ChoiceValidator().GetAvailability(o, profile).CanSelect)
                .Select(o => o.Id).Order(StringComparer.Ordinal).ToImmutableArray();
            return new PlanningChange(revision.StudentId, revision.Id, choice, validation, removed, removed.IsEmpty ? [] : replacements);
        }).ToImmutableArray();
        return new(basis, catalog, ids, changes, restoring);
    }

    public static PlanningDocument Scenario(CancellationPreview preview, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Szenarioname fehlt.");
        return preview.Basis with { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, Name = name.Trim(), IsScenario = true,
            BasisId = preview.Basis.Id, Catalog = preview.Catalog,
            Choices = [.. preview.Basis.Choices.Select(r => r with { Id = Guid.NewGuid(), PreviousRevisionId = r.Id,
                ActorRole = ActorRole.System, Reason = "Szenario: " + name.Trim(), ChangedAt = DateTimeOffset.UtcNow,
                Source = preview.Restoring ? RevisionSource.Restoration : RevisionSource.OfferCancellation,
                OfferVersion = preview.Catalog.Version, RuleVersion = ChoiceValidator.Version,
                Changes = [.. preview.Changes.Single(c => c.StudentId == r.StudentId).RemovedOffers.Select(id => new SelectionChange(id, r.Snapshot.Selections.Single(s => s.OfferId == id).Semesters, []))],
                Snapshot = preview.Changes.Single(c => c.StudentId == r.StudentId).Choice })] };
    }

    public static void Confirm(IPlanningStore store, CancellationPreview preview, string actor, string reason, bool confirmed)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Bestätigte Vorschau, Bearbeiter und Begründung erforderlich.");
        var revisions = preview.Changes.Select(c => new ChoiceRevision(Guid.NewGuid(), c.ExpectedActive, c.StudentId, DateTimeOffset.UtcNow, actor,
            ActorRole.Teacher, preview.Restoring ? RevisionSource.Restoration : RevisionSource.OfferCancellation, reason.Trim(), preview.Catalog.Version, ChoiceValidator.Version, c.Choice,
            [.. c.RemovedOffers.Select(id => new SelectionChange(id, preview.Basis.Choices.Single(r => r.StudentId == c.StudentId).Snapshot.Selections.Single(s => s.OfferId == id).Semesters, []))], null)).ToImmutableArray();
        var followUps = preview.Changes.Where(c => !c.RemovedOffers.IsEmpty).Select(c =>
        {
            var old = preview.Basis.FollowUps.Single(f => f.StudentId == c.StudentId);
            return old with { Required = old.Required || c.Validation.Status != ReviewStatus.Valid, Note = actor + ": " + reason.Trim() + " · Prüfung: " + c.Validation.Status };
        }).ToImmutableArray();
        store.CommitCancellation(preview, actor, reason.Trim(), revisions, followUps);
    }
}
