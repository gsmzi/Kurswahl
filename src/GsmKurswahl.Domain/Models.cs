using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

public enum CourseType { Lf, Bf, Elective, Seminar }
public enum CourseVariant { Normal, Bilingual }
public enum CooperationSchool { None, Ewg, Gsm }
public enum OfferStatus { Planned, Offered, Cancelled, Established }
public enum ReviewStatus { Valid, Invalid, NeedsReview }
public enum Knowledge { Unknown, Yes, No }
public enum SubmissionStatus { Draft, Submitted }
public enum ElectionPhase { Preliminary, Final }
public enum DepartureStatus { Active, ExpectedDeparture, ConfirmedDeparture }
public enum ActorRole { Student, Teacher, System }
public enum RevisionSource { StudentSubmission, TeacherEdit, OfferCancellation, AsvReconciliation, Restoration }
public enum ProfileStatus { Missing, Unverified, Verified, ProfileInvalid }
public enum Requirement { Informatics, ItalianProfile, ContinuousBilingual }

public sealed record Subject(string Id, string Name)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? TaskField { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsForeignLanguage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsScience { get; init; }
}
public sealed record SemesterHours(int Semester, int WeeklyHours);
public sealed record CourseOffer(string Id, string SubjectId, CourseType Type, CourseVariant Variant,
    OfferStatus Status, ImmutableArray<SemesterHours> Schedule, ImmutableArray<Requirement> Requirements)
{
    // Catalog v3: descriptive school annotations only; never eligibility, hours or assignment.
    [System.Text.Json.Serialization.JsonRequired]
    public CooperationSchool CooperationSchool { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public bool IsTopUpCourse { get; init; }
}
public sealed record OfferCatalog(int SchemaVersion, string Version, int GraduationYear, bool IsDemo,
    ImmutableArray<Subject> Subjects, ImmutableArray<CourseOffer> Offers)
{
    // Catalog v2: only a hint for pupils, set in the distributed copy; collection is a separate teacher action.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? SubmissionDeadline { get; init; }
}
public sealed record CourseSelection(string OfferId, string SubjectId, CourseType Type,
    CourseVariant Variant, ImmutableArray<int> Semesters);

// This document is an untrusted student draft, never a source of eligibility or administrative truth.
public sealed record CourseChoice(int SchemaVersion, Guid DocumentId, string StudentId, int GraduationYear,
    string OfferVersion, string RuleVersion, int Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    SubmissionStatus SubmissionStatus, ElectionPhase Phase, ImmutableArray<CourseSelection> Selections,
    ImmutableArray<string> PlannedOralSubjects);

public sealed record PriorEducation(Knowledge ItalianProfile, Knowledge InformaticsBridge,
    Knowledge InformaticsImp, Knowledge ContinuousBilingual, string? ReligionEthicsGrade10,
    ImmutableArray<int>? SportExemptSemesters)
{
    // Administrative confirmations, never inferred from a student choice or language name.
    public ImmutableArray<string> ContinuedLanguages { get; init; } = [];
    public Knowledge ReligionEthicsPermission { get; init; } = Knowledge.Unknown;
    public Knowledge ReligionEthicsExamPermission { get; init; } = Knowledge.Unknown;
    public Knowledge PermanentSportExemption { get; init; } = Knowledge.Unknown;
    public static PriorEducation Unknown { get; } = new(Knowledge.Unknown, Knowledge.Unknown,
        Knowledge.Unknown, Knowledge.Unknown, null, null);
}

// Advisory copy of protected prerequisites for the pupil app; binding checks always use the teacher stock.
public sealed record StudentProfile(int SchemaVersion, int ProfileRevision, string StudentId,
    string WindowsUsername, int GraduationYear, DateTimeOffset IssuedAt, PriorEducation Prerequisites)
{
    public string DisplayName { get; init; } = "";
    public string ClassName { get; init; } = "";
}
public sealed record ProfileContext(ProfileStatus Status, PriorEducation Prerequisites)
{
    public int? Revision { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public string Detail { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public static ProfileContext Missing { get; } = new(ProfileStatus.Missing, PriorEducation.Unknown);
}
public sealed record CurrentIdentity(string Username, string? AccountId);
public interface ICurrentIdentityProvider { CurrentIdentity GetCurrent(); }

public sealed record RuleDefinition(string Id, string Description, string Source, string Scope,
    string Version, ReviewStatus Severity);
public sealed record RuleMessage(RuleDefinition Rule, ImmutableArray<string> SubjectIds,
    ImmutableArray<int> Semesters, string Explanation, string Remedy);
public sealed record ValidationResult(ReviewStatus Status, ImmutableArray<RuleMessage> Messages);
public sealed record SemesterSummary(int Semester, int CourseCount, int WeeklyHours);
public sealed record ExamOption(string First, string Second, int MinimumCredits, ReviewStatus Status, string Note);

// Protected teacher storage in M3: these are separate from CourseChoice and never written to pupil homes.
public sealed record ImportOrigin(string SourceId, string ContentHash, string MappingVersion,
    DateTimeOffset ImportedAt, string AuthenticatedAccount);
public sealed record AdministrativeStudent(Guid Id, string ExternalStudentId, string SyntheticDisplayName,
    string ClassName, int GraduationYear, DepartureStatus Departure, PriorEducation Prerequisites)
{
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public (string Last, string First) SortName()
    {
        if (!string.IsNullOrWhiteSpace(LastName) || !string.IsNullOrWhiteSpace(FirstName))
            return (LastName, FirstName);
        var comma = SyntheticDisplayName.IndexOf(',');
        if (comma >= 0)
            return (SyntheticDisplayName[..comma].Trim(), SyntheticDisplayName[(comma + 1)..].Trim());
        var space = SyntheticDisplayName.LastIndexOf(' ');
        return space > 0
            ? (SyntheticDisplayName[(space + 1)..].Trim(), SyntheticDisplayName[..space].Trim())
            : (SyntheticDisplayName, "");
    }
}
public sealed record SelectionChange(string OfferId, ImmutableArray<int> Before, ImmutableArray<int> After);
public sealed record ChoiceRevision(Guid Id, Guid? PreviousRevisionId, string StudentId,
    DateTimeOffset ChangedAt, string ActorId, ActorRole ActorRole, RevisionSource Source, string Reason,
    string OfferVersion, string RuleVersion, CourseChoice Snapshot, ImmutableArray<SelectionChange> Changes,
    ImportOrigin? ImportOrigin);
public sealed record AdministrativeChoice(Guid Id, string StudentId, Guid ActiveRevisionId,
    Guid OriginalStudentRevisionId, bool RequiresReselection);
public sealed record CourseGroup(Guid Id, string OfferId, int Semester, string ExternalGroupCode);
public sealed record CourseAssignment(Guid GroupId, string StudentId);
public sealed record PlanningSnapshot(Guid Id, DateTimeOffset CreatedAt, OfferCatalog Catalog,
    ImmutableArray<ChoiceRevision> Choices, ImmutableArray<AdministrativeStudent> Students,
    bool IncludeExpectedDepartures);
public sealed record PlanningScenario(Guid Id, Guid BaseSnapshotId, ImmutableArray<string> CancelledOfferIds);

public sealed record StoredChoice(CourseChoice Choice, string ContentToken)
{
    // Set when the file uses an older schema and was converted in memory; the file itself is unchanged.
    public int? UpgradedFromSchema { get; init; }
}
public interface IChoiceStore
{
    Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default);
    // null token means create only. A previous load/save token is required for replacement.
    Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default);
}
