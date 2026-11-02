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
// Database schema 4: byte-exact archive of pupil files before they are removed from a pupil folder.
public sealed record CollectedFile(int SchemaVersion, Guid Id, string StudentId, DateTimeOffset At, string Actor,
    string FileName, string Sha256, byte[] Content);
public sealed record DeliveredFile(string Path, string Sha256);
// Files the teacher app itself copied into a pupil folder; only unchanged copies are removed again.
public sealed record ProgramDelivery(int SchemaVersion, Guid Id, string StudentId, DateTimeOffset At, string Directory,
    ImmutableArray<DeliveredFile> Files);
// Deletion log without name or school ID; only the internal key, so a deletion stays traceable but not personal.
public sealed record StudentDeletion(int SchemaVersion, Guid Id, Guid InternalStudentId, DateTimeOffset At, string Actor,
    string Reason, int RemovedRows, int UpdatedPlanningSnapshots);
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
