using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

// Database schema 4: archive before removal from pupil folders, program deliveries and deliberate deletion.
public sealed partial class SqliteAdministrationStore
{
    public void ArchiveCollectedFiles(IReadOnlyCollection<CollectedFile> files)
    {
        foreach (var file in files)
            if (file.SchemaVersion != 1 || file.Id == Guid.Empty || string.IsNullOrWhiteSpace(file.StudentId) || string.IsNullOrWhiteSpace(file.Actor)
                || string.IsNullOrWhiteSpace(file.FileName) || file.Sha256 != Convert.ToHexString(SHA256.HashData(file.Content)))
                throw new ArgumentException("Ungültiger Archiveintrag.");
        using var tx = db.BeginTransaction();
        foreach (var file in files) Execute("INSERT INTO collected_files(student,body) VALUES($p0,$p1)", file.StudentId, Json(file));
        tx.Commit();
    }
    public ImmutableArray<CollectedFile> CollectedFiles(string studentId)
    {
        var rows = Read<CollectedFile>("SELECT body FROM collected_files WHERE student=$p0 ORDER BY seq", studentId);
        if (rows.Any(r => r.SchemaVersion != 1)) throw new InvalidDataException("Unbekannte Archivversion.");
        return rows;
    }
    public void RecordProgramDelivery(ProgramDelivery delivery)
    {
        if (delivery.SchemaVersion != 1 || delivery.Id == Guid.Empty || string.IsNullOrWhiteSpace(delivery.StudentId) || delivery.Files.IsDefault)
            throw new ArgumentException("Ungültiger Verteilungsnachweis.");
        Execute("INSERT INTO program_deliveries(student,body) VALUES($p0,$p1)", delivery.StudentId, Json(delivery));
    }
    public ProgramDelivery? LatestProgramDelivery(string studentId)
    {
        var row = Read<ProgramDelivery>("SELECT body FROM program_deliveries WHERE student=$p0 ORDER BY seq DESC LIMIT 1", studentId).FirstOrDefault();
        if (row is not null && row.SchemaVersion != 1) throw new InvalidDataException("Unbekannte Verteilungsversion.");
        return row;
    }
    public ImmutableArray<StudentDeletion> Deletions() => Read<StudentDeletion>("SELECT body FROM deletions ORDER BY seq");

    /// <summary>
    /// Deliberately removes one person from this year's protected stock: master data, choice history, follow-ups,
    /// profiles, delivery reports, transfer receipts, archived files and program deliveries; stored planning
    /// snapshots are rewritten without the person. Only a person-free log entry remains. Pupil folders are not touched.
    /// </summary>
    public StudentDeletion DeleteStudent(string studentId, Guid expectedInternalId, string actor, string reason)
    {
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Bearbeiter und Löschgrund erforderlich.");
        using var tx = db.BeginTransaction();
        var record = Students().SingleOrDefault(s => s.Student.ExternalStudentId == studentId)
            ?? throw new InvalidOperationException("Person nicht (mehr) vorhanden.");
        if (record.Student.Id != expectedInternalId) throw new InvalidOperationException("Stammdaten geändert; Ansicht erneuern.");
        foreach (var table in Tables) Execute($"DROP TRIGGER IF EXISTS {table}_no_update; DROP TRIGGER IF EXISTS {table}_no_delete;");
        var removed = 0;
        foreach (var (table, column) in new[] { ("students", "id"), ("revisions", "student"), ("reselections", "student"), ("profiles", "student"),
            ("collected_files", "student"), ("program_deliveries", "student") })
        {
            using var command = Command($"DELETE FROM {table} WHERE {column}=$p0", studentId);
            removed += command.ExecuteNonQuery();
        }
        // Tables without a student column: match the parsed body, never a text search.
        foreach (var seq in ReadRows<DistributionEntry>("distributions").Where(r => r.Value.StudentId == studentId).Select(r => r.Seq)
            .Select(s => ("distributions", s)).Concat(ReadRows<TransferReceipt>("transfer_receipts").Where(r => r.Value.StudentId == studentId).Select(r => ("transfer_receipts", r.Seq))))
        {
            using var command = Command($"DELETE FROM {seq.Item1} WHERE seq=$p0", seq.Item2);
            removed += command.ExecuteNonQuery();
        }
        var snapshots = 0;
        foreach (var (seq, document) in ReadRows<PlanningDocument>("planning_snapshots"))
        {
            if (!document.Students.Any(s => s.Student.ExternalStudentId == studentId) && !document.Choices.Any(c => c.StudentId == studentId)) continue;
            var reduced = document with
            {
                Students = [.. document.Students.Where(s => s.Student.ExternalStudentId != studentId)],
                Choices = [.. document.Choices.Where(c => c.StudentId != studentId)],
                FollowUps = [.. document.FollowUps.Where(f => f.StudentId != studentId)]
            };
            ValidatePlanning(reduced);
            Execute("UPDATE planning_snapshots SET body=$p1 WHERE seq=$p0", seq, Json(reduced));
            snapshots++;
        }
        var log = new StudentDeletion(1, Guid.NewGuid(), record.Student.Id, DateTimeOffset.UtcNow, actor.Trim(), reason.Trim(), removed, snapshots);
        Execute("INSERT INTO deletions(body) VALUES($p0)", Json(log));
        CreateAppendOnlyTriggers();
        tx.Commit();
        return log;
    }
    private ImmutableArray<(long Seq, T Value)> ReadRows<T>(string table)
    {
        using var command = Command($"SELECT seq, body FROM {table} ORDER BY seq"); using var reader = command.ExecuteReader();
        var rows = ImmutableArray.CreateBuilder<(long, T)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), JsonSerializer.Deserialize<T>(reader.GetString(1), DocumentJson.Options) ?? throw new InvalidDataException("Leerer Verwaltungsdatensatz.")));
        return rows.ToImmutable();
    }
}
