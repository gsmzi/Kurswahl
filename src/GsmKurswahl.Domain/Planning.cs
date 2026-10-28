using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

public sealed record PlanningFollowUp(string StudentId, bool Required, DateTimeOffset? Deadline, string Note);
public sealed record PlanningDocument(int SchemaVersion, Guid Id, DateTimeOffset CreatedAt, string Name, string Actor,
    string RuleVersion, OfferCatalog Catalog, ImmutableArray<StudentRecord> Students,
    ImmutableArray<ChoiceRevision> Choices, ImmutableArray<PlanningFollowUp> FollowUps, bool IncludeExpectedDepartures,
    int TargetGroupSize = 24, bool IsScenario = false, Guid? BasisId = null);
public sealed record PlanningChange(string StudentId, Guid ExpectedActive, CourseChoice Choice,
    ValidationResult Validation, ImmutableArray<string> RemovedOffers, ImmutableArray<string> ReplacementOffers);
public sealed record CancellationPreview(PlanningDocument Basis, OfferCatalog Catalog,
    ImmutableArray<string> CancelledOffers, ImmutableArray<PlanningChange> Changes, bool Restoring = false);
public sealed record DemandRow(string OfferId, int Semester, int All, int Departures, int Relevant, int EstimatedGroups);
public sealed record CombinationRow(string Combination, int All, int Departures, int Relevant);
public sealed record OverlapRow(string FirstOffer, string SecondOffer, int Semester, int All, int Departures, int Relevant);
public sealed record PlanningAnalysis(int All, int ExpectedDepartures, int ConfirmedDepartures, int Relevant,
    int WithoutChoice, ImmutableArray<DemandRow> Demand, ImmutableArray<CombinationRow> Combinations,
    ImmutableArray<OverlapRow> Overlaps);
public interface IPlanningStore
{
    bool PlanningEnabled { get; }
    // Explicit, user-triggered upgrade; existing databases are never silently migrated.
    void EnablePlanning();
    ImmutableArray<PlanningDocument> PlanningDocuments();
    void SavePlanningDocument(PlanningDocument document);
    OfferCatalog? ActivePlanningCatalog();
    DateTimeOffset? PlanningCatalogChangedAt();
    void CommitCancellation(CancellationPreview preview, string actor, string reason,
        ImmutableArray<ChoiceRevision> revisions, ImmutableArray<PlanningFollowUp> followUps);
}
