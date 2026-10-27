using System.Collections.Immutable;
using System.Text.Json;
using GsmKurswahl.Domain;
using Microsoft.Data.Sqlite;

namespace GsmKurswahl.AdministrationStorage;

/// <summary>One local writer. SQLite transactions and immutable rows protect administrative history.</summary>
public sealed class SqliteAdministrationStore : IAdministrationStore
{
    private readonly FileStream lease;
    private readonly SqliteConnection db;
    public SqliteAdministrationStore(string path)
    {
        path = Path.GetFullPath(path);
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new ArgumentException("Verwaltungsdatenbank muss lokal liegen; kein SMB/Netzlaufwerk.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            db.Open();
            var version = Convert.ToInt32(Scalar("PRAGMA user_version"));
            if (version is not (0 or 1)) throw new InvalidDataException("Unbekannte Verwaltungsschemaversion; keine Migration.");
            if (version == 0)
            {
                if (Convert.ToInt32(Scalar("SELECT count(*) FROM sqlite_master WHERE type='table'")) != 0)
                    throw new InvalidDataException("Unversionierte Datenbank wird nicht verändert.");
                Execute("""
                    BEGIN IMMEDIATE;
                    CREATE TABLE students (id TEXT NOT NULL, version INTEGER NOT NULL, body TEXT NOT NULL, PRIMARY KEY(id,version));
                    CREATE TABLE revisions (seq INTEGER PRIMARY KEY, id TEXT NOT NULL UNIQUE, student TEXT NOT NULL, body TEXT NOT NULL);
                    CREATE TABLE reselections (seq INTEGER PRIMARY KEY, student TEXT NOT NULL, body TEXT NOT NULL);
                    CREATE TABLE profiles (student TEXT NOT NULL, revision INTEGER NOT NULL, body TEXT NOT NULL, PRIMARY KEY(student,revision));
                    CREATE TABLE distributions (seq INTEGER PRIMARY KEY, body TEXT NOT NULL);
                    PRAGMA user_version=1;
                    COMMIT;
                    """);
            }
            foreach (var table in new[] { "students", "revisions", "reselections", "profiles", "distributions" })
                Execute($"CREATE TRIGGER IF NOT EXISTS {table}_no_update BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT,'append-only'); END; CREATE TRIGGER IF NOT EXISTS {table}_no_delete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT,'append-only'); END;");
        }
        catch { db.Dispose(); lease.Dispose(); throw; }
    }
    private SqliteCommand Command(string sql, params object?[] args)
    {
        var command = db.CreateCommand(); command.CommandText = sql;
        for (var i = 0; i < args.Length; i++) command.Parameters.AddWithValue("$p" + i, args[i] ?? DBNull.Value);
        return command;
    }
    private object? Scalar(string sql, params object?[] args) { using var c = Command(sql, args); return c.ExecuteScalar(); }
    private void Execute(string sql, params object?[] args) { using var c = Command(sql, args); c.ExecuteNonQuery(); }
    private ImmutableArray<T> Read<T>(string sql, params object?[] args)
    {
        using var c = Command(sql, args); using var reader = c.ExecuteReader();
        var result = ImmutableArray.CreateBuilder<T>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), DocumentJson.Options) ?? throw new InvalidDataException("Leerer Verwaltungsdatensatz."));
        return result.ToImmutable();
    }
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, DocumentJson.Options);
    public ImmutableArray<StudentRecord> Students()
    {
        var records = Read<StudentRecord>("SELECT body FROM students s WHERE version=(SELECT MAX(version) FROM students WHERE id=s.id) ORDER BY id");
        if (records.Any(r => r.SchemaVersion != 1)) throw new InvalidDataException("Unbekannte Stammdaten-Schemaversion.");
        return records;
    }
    public void SaveStudent(StudentRecord record, int? expectedVersion)
    {
        if (record.SchemaVersion != 1 || record.Version != (expectedVersion ?? 0) + 1 || string.IsNullOrWhiteSpace(record.Actor)
            || !record.Student.ExternalStudentId.StartsWith("demo-", StringComparison.Ordinal) || record.Student.Id == Guid.Empty
            || record.Student.GraduationYear is < 2000 or > 2200 || string.IsNullOrWhiteSpace(record.Student.SyntheticDisplayName))
            throw new ArgumentException("Ungültige Stammdaten; dieser Entwicklungsstand erlaubt nur synthetische demo-IDs.");
        if (Path.IsPathFullyQualified(record.HomeDirectory)) record = record with { HomeDirectory = Path.GetFullPath(record.HomeDirectory) };
        var all = Students();
        var existing = all.SingleOrDefault(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
        if (existing is not null && existing.Student.Id != record.Student.Id) throw new InvalidOperationException("Interne Schüler-ID darf nicht geändert werden.");
        var others = all.Where(s => s.Student.ExternalStudentId != record.Student.ExternalStudentId);
        if (others.Any(s => s.Student.Id == record.Student.Id || (!string.IsNullOrWhiteSpace(record.WindowsUsername) && string.Equals(s.WindowsUsername, record.WindowsUsername, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(record.AccountId) && s.AccountId == record.AccountId)
            || (!string.IsNullOrWhiteSpace(record.HomeDirectory) && string.Equals(s.HomeDirectory.TrimEnd('\\','/'), record.HomeDirectory.TrimEnd('\\','/'), StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Schüler-ID, Konto, SID oder Home bereits einer anderen Person zugeordnet.");
        using var tx = db.BeginTransaction();
        var current = Scalar("SELECT MAX(version) FROM students WHERE id=$p0", record.Student.ExternalStudentId);
        if ((current is null or DBNull ? (int?)null : Convert.ToInt32(current)) != expectedVersion) throw new InvalidOperationException("Stammdaten wurden inzwischen geändert.");
        Execute("INSERT INTO students VALUES($p0,$p1,$p2)", record.Student.ExternalStudentId, record.Version, Json(record)); tx.Commit();
    }
    public ImmutableArray<ChoiceRevision> History(string studentId) => Read<ChoiceRevision>("SELECT body FROM revisions WHERE student=$p0 ORDER BY seq", studentId);
    public void Append(ChoiceRevision revision, Guid? expectedActive)
    {
        ModelGuard.Choice(revision.Snapshot);
        if (revision.StudentId != revision.Snapshot.StudentId || revision.PreviousRevisionId != expectedActive || string.IsNullOrWhiteSpace(revision.ActorId) || string.IsNullOrWhiteSpace(revision.Reason))
            throw new ArgumentException("Ungültige Verwaltungsrevision.");
        using var tx = db.BeginTransaction();
        if (History(revision.StudentId).LastOrDefault()?.Id != expectedActive) throw new InvalidOperationException("Wahl wurde inzwischen geändert; Vorschau erneuern.");
        Execute("INSERT INTO revisions(id,student,body) VALUES($p0,$p1,$p2)", revision.Id.ToString(), revision.StudentId, Json(revision)); tx.Commit();
    }
    private sealed record ReselectionData(bool Required, DateTimeOffset? Deadline, string Note, DateTimeOffset At);
    public void SetReselection(string studentId, bool required, DateTimeOffset? deadline, string note) =>
        Execute("INSERT INTO reselections(student,body) VALUES($p0,$p1)", studentId, Json(new ReselectionData(required, deadline, note, DateTimeOffset.UtcNow)));
    public (bool Required, DateTimeOffset? Deadline, string Note) Reselection(string studentId)
    {
        var data = Read<ReselectionData>("SELECT body FROM reselections WHERE student=$p0 ORDER BY seq DESC LIMIT 1", studentId).FirstOrDefault();
        return data is null ? (false, null, "") : (data.Required, data.Deadline, data.Note);
    }
    public IssuedProfile? LatestProfile(string studentId) => Read<IssuedProfile>("SELECT body FROM profiles WHERE student=$p0 ORDER BY revision DESC LIMIT 1", studentId).FirstOrDefault();
    public void SaveProfile(IssuedProfile profile)
    {
        if (profile.Profile.ProfileRevision != (LatestProfile(profile.Profile.StudentId)?.Profile.ProfileRevision ?? 0) + 1) throw new InvalidOperationException("Profilrevision ist nicht fortlaufend.");
        Execute("INSERT INTO profiles VALUES($p0,$p1,$p2)", profile.Profile.StudentId, profile.Profile.ProfileRevision, Json(profile));
    }
    public void LogDistribution(DistributionEntry entry) => Execute("INSERT INTO distributions(body) VALUES($p0)", Json(entry));
    public ImmutableArray<DistributionEntry> DistributionLog() => Read<DistributionEntry>("SELECT body FROM distributions ORDER BY seq");
    public void Dispose() { db.Dispose(); lease.Dispose(); }
}
