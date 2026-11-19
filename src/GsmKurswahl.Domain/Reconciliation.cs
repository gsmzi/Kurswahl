using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

// Normalized external data, never eligibility evidence. No ASV column names are assumed.
public sealed record ExternalChoice(string StudentId, int GraduationYear, ImmutableArray<CourseSelection> Selections)
{
    public ImmutableArray<string> PlannedOralSubjects { get; init; } = [];
    public bool CompareOralSubjects { get; init; }
    public bool IgnoreBilingualVariant { get; init; }
    public string DisplayName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public ImmutableArray<string> Warnings { get; init; } = [];
}
public enum ReconciliationKind { Identical, Changed, OnlyInternal, OnlyExternal, InvalidIdentity }
public sealed record ReconciliationItem(string StudentId, ReconciliationKind Kind, StudentRecord? Student,
    ChoiceRevision? Internal, ExternalChoice? External, ValidationResult? Validation, string Detail)
{
    public ImmutableArray<string> Differences { get; init; } = [];
    public ImmutableArray<string> Notes { get; init; } = [];
}
public sealed record ReconciliationPreview(ImportOrigin Origin, string CatalogVersion,
    ImmutableArray<ReconciliationItem> Items);
