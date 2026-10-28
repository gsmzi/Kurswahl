using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GsmKurswahl.Domain;
using Microsoft.Data.Sqlite;

namespace GsmKurswahl.AdministrationStorage;

public sealed record TransferManifest(int SchemaVersion, DateTimeOffset CreatedAt, TeacherYear Year, string DatabaseHash);

/// <summary>One active year, including all SQLite history. Never copies student home directories.</summary>
public static class WorkspaceTransfer
{
    public static void Export(TeacherYear year, SqliteAdministrationStore store, string destination)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var database = Path.Combine(Path.GetTempPath(), "gsm-transfer-" + Guid.NewGuid() + ".db");
        try
        {
            store.BackupTo(database);
            var bytes = File.ReadAllBytes(database);
            var manifest = new TransferManifest(1, DateTimeOffset.UtcNow, year with { DatabasePath = "verwaltung.db" }, Convert.ToHexString(SHA256.HashData(bytes)));
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
                {
                    using (var entry = zip.CreateEntry("transfer.json").Open()) JsonSerializer.Serialize(entry, manifest, DocumentJson.Options);
                    using (var entry = zip.CreateEntry("verwaltung.db").Open()) entry.Write(bytes);
                }
                file.Flush(true);
            }
            File.Move(temporary, destination, false);
        }
        finally { File.Delete(temporary); File.Delete(database); }
    }

    private static (TransferManifest Manifest, byte[] Database) Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count != 2 || zip.Entries.Count(e => e.FullName == "transfer.json") != 1 || zip.Entries.Count(e => e.FullName == "verwaltung.db") != 1)
            throw new InvalidDataException("Transportdatei muss genau Manifest und Verwaltungsdatenbank enthalten.");
        byte[] Entry(string name, long limit)
        {
            var entry = zip.GetEntry(name)!;
            if (entry.Length > limit) throw new InvalidDataException("Transportdatei zu groß.");
            using var stream = entry.Open(); var bytes = new byte[checked((int)entry.Length)]; stream.ReadExactly(bytes); return bytes;
        }
        var manifest = JsonSerializer.Deserialize<TransferManifest>(Entry("transfer.json", 4 * 1024 * 1024), DocumentJson.Options)
            ?? throw new InvalidDataException("Leere Transportdatei.");
        if (manifest.SchemaVersion != 1 || manifest.Year is null || manifest.Year.DatabasePath != "verwaltung.db" || !Enum.IsDefined(manifest.Year.Step)
            || !manifest.Year.Catalog.IsDemo || string.IsNullOrWhiteSpace(manifest.Year.Actor))
            throw new InvalidDataException("Unbekannte Transportversion oder ungültiger Inhalt.");
        ModelGuard.Catalog(manifest.Year.Catalog);
        var database = Entry("verwaltung.db", 512L * 1024 * 1024);
        if (Convert.ToHexString(SHA256.HashData(database)) != manifest.DatabaseHash) throw new InvalidDataException("Transportdatei beschädigt (Prüfsumme).");
        return (manifest, database);
    }
    public static TransferManifest Inspect(string path) => Read(path).Manifest;

    // Always import to a fresh local folder. Old workspaces are preserved, never merged or overwritten.
    public static TeacherWorkspace Import(string path, string newRoot, TransferManifest? expected = null)
    {
        var (manifest, bytes) = Read(path);
        if (expected is not null && JsonSerializer.Serialize(manifest, DocumentJson.Options) != JsonSerializer.Serialize(expected, DocumentJson.Options))
            throw new IOException("Transportdatei wurde nach der Vorschau geändert. Bitte erneut öffnen und prüfen.");
        var workspace = new TeacherWorkspace(newRoot);
        if (Directory.Exists(newRoot)) throw new IOException("Für die Übernahme ist ein neues Verzeichnis erforderlich.");
        Directory.CreateDirectory(newRoot);
        var database = Path.Combine(newRoot, "verwaltung.db");
        using (var output = new FileStream(database, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
        using (var check = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            check.Open(); using var command = check.CreateCommand(); command.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(command.ExecuteScalar()) is not (1 or 2)) throw new InvalidDataException("Unbekannte oder unversionierte Transportdatenbank; keine Migration.");
        }
        // Attach validates DB schema and graduation year before registering it.
        workspace.Attach(manifest.Year.Catalog, database, manifest.Year.Actor);
        workspace.Update(manifest.Year with { DatabasePath = Path.GetFullPath(database) });
        return workspace;
    }
}

public sealed partial class SqliteAdministrationStore
{
    public void BackupTo(string newPath)
    {
        using (new FileStream(newPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = newPath, Pooling = false }.ToString());
        target.Open(); db.BackupDatabase(target);
    }
}
