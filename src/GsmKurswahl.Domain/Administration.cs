using System.Collections.Immutable;

namespace GsmKurswahl.Domain;

// Independent, versioned protected administrative records; never included in pupil choices.
public sealed record StudentRecord(int SchemaVersion, AdministrativeStudent Student, string WindowsUsername,
    string AccountId, string HomeDirectory, bool MappingConfirmed, int Version, string Actor,
    DateTimeOffset ChangedAt, ImportOrigin? Origin);
public sealed record TeacherState(StudentRecord Record, ChoiceRevision? Active, ValidationResult? Validation,
    bool RequiresReselection, DateTimeOffset? ReselectionDeadline, string Note);
public sealed record DistributionEntry(Guid Id, string StudentId, int ProfileRevision, DateTimeOffset At,
    string Status, string Detail);
public sealed record IssuedProfile(StudentProfile Profile, byte[] ProfileBytes, byte[] SignatureBytes);
public interface IAdministrationStore : IDisposable
{
    ImmutableArray<StudentRecord> Students();
    ImmutableArray<StudentRecord> StudentHistory(string studentId);
    void SaveStudent(StudentRecord record, int? expectedVersion);
    ImmutableArray<ChoiceRevision> History(string studentId);
    void Append(ChoiceRevision revision, Guid? expectedActive);
    void SetReselection(string studentId, bool required, DateTimeOffset? deadline, string note);
    (bool Required, DateTimeOffset? Deadline, string Note) Reselection(string studentId);
    IssuedProfile? LatestProfile(string studentId);
    void SaveProfile(IssuedProfile profile);
    void LogDistribution(DistributionEntry entry);
    ImmutableArray<DistributionEntry> DistributionLog();
}
