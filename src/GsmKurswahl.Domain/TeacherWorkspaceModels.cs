using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

public enum YearSetupStep { Offer, Import, Mapping, Review, Complete }
// Workspace v2 (02.11.2026): without the former certificate fields, with deadline hint and pupil program folder.
public sealed record TeacherYear(OfferCatalog Catalog, string DatabasePath, YearSetupStep Step, string Actor)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? SubmissionDeadline { get; init; }
    // Local folder with the published pupil program; copied into each pupil folder on distribution when set.
    public string StudentProgramDirectory { get; init; } = "";
}
public sealed record TeacherWorkspaceDocument(int SchemaVersion, int? ActiveYear, ImmutableArray<TeacherYear> Years);
