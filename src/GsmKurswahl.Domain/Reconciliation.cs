using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

// Normalized external data, never eligibility evidence. No ASV column names are assumed.
public sealed record ExternalChoice(string StudentId, int GraduationYear, ImmutableArray<CourseSelection> Selections);
public enum ReconciliationKind { Identical, Changed, OnlyInternal, OnlyExternal, InvalidIdentity }
public sealed record ReconciliationItem(string StudentId, ReconciliationKind Kind, StudentRecord? Student,
    ChoiceRevision? Internal, ExternalChoice? External, ValidationResult? Validation, string Detail);
public sealed record ReconciliationPreview(ImportOrigin Origin, string CatalogVersion,
    ImmutableArray<ReconciliationItem> Items);
