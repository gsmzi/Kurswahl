using System.Security.Cryptography;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed record CleanupResult(string StudentId, string Status, string Detail);

/// <summary>
/// Final collection: empties a confirmed pupil folder after the choice is in the protected stock. Every data file is
/// archived byte-exactly in the database first; program files are removed only if unchanged since our own delivery.
/// Anything not provably saved stays in place.
/// </summary>
public static class StudentFolderCleanup
{
    public const string ProgramFolder = "Kurswahl-Programm";
    private static readonly string[] DataFiles = ["aktuell.json", "aktuell.json.bak", "profil.json", "fachangebot.json"];
    private static readonly string[] LockFiles = ["aktuell.json.lock", ".profil.lock"];
    private const long MaximumBytes = 2 * 1024 * 1024;

    // acceptedChoiceTokens: content hashes of pupil files a teacher explicitly dismissed in favour of the teacher stock.
    public static CleanupResult Clean(SqliteAdministrationStore store, StudentRecord record, string actor, IReadOnlySet<string> acceptedChoiceTokens)
    {
        var id = record.Student.ExternalStudentId;
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Bearbeiterkennung erforderlich.");
        try
        {
            if (!record.MappingConfirmed || !Path.IsPathFullyQualified(record.HomeDirectory) || !Directory.Exists(record.HomeDirectory))
                return new(id, "nicht_gefunden", "Bestätigter Wahlordner nicht erreichbar; nichts entfernt.");
            var found = new List<(string Path, byte[] Bytes)>();
            foreach (var name in DataFiles)
            {
                var path = Path.Combine(record.HomeDirectory, name);
                if (!File.Exists(path)) continue;
                if (new FileInfo(path).Length > MaximumBytes) return new(id, "nicht_eingesammelt", name + " ist ungewöhnlich groß; nichts entfernt.");
                found.Add((path, File.ReadAllBytes(path)));
            }
            var choice = found.FirstOrDefault(f => Path.GetFileName(f.Path) == "aktuell.json");
            if (choice.Bytes is not null)
            {
                var token = Token(choice.Bytes);
                if (!store.History(id).Any(r => r.ImportOrigin?.ContentHash == token) && !acceptedChoiceTokens.Contains(token))
                    return new(id, "nicht_eingesammelt", "Aktuelle Schülerwahl ist noch nicht im Lehrerbestand (oder offener Konflikt); nichts entfernt.");
            }
            var archive = found.Select(f => new CollectedFile(1, Guid.NewGuid(), id, DateTimeOffset.UtcNow, actor,
                Path.GetFileName(f.Path), Token(f.Bytes), f.Bytes)).ToArray();
            store.ArchiveCollectedFiles(archive);
            var stored = store.CollectedFiles(id);
            if (archive.Any(a => !stored.Any(s => s.Id == a.Id && s.Content.AsSpan().SequenceEqual(a.Content))))
                return new(id, "Archivfehler", "Archiv konnte nicht bestätigt werden; nichts entfernt.");
            var kept = new List<string>();
            foreach (var (path, bytes) in found)
            {
                // Recheck right before removal: a file changed after archiving stays in place.
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) File.Delete(path);
                else if (File.Exists(path)) kept.Add(Path.GetFileName(path));
            }
            foreach (var name in LockFiles)
            {
                var path = Path.Combine(record.HomeDirectory, name);
                if (File.Exists(path) && new FileInfo(path).Length == 0) File.Delete(path);
            }
            kept.AddRange(RemoveProgram(store, record));
            return kept.Count == 0
                ? new(id, "geräumt", $"{archive.Length} Datei(en) archiviert und entfernt; Programm entfernt.")
                : new(id, "teilweise", "Archiviert; unverändert belassen: " + string.Join(", ", kept));
        }
        catch (UnauthorizedAccessException) { return new(id, "Zugriff_verweigert", "Keine Schreibberechtigung im Wahlordner."); }
        catch (IOException ex) { return new(id, "Zugriff_verweigert", "Datei gesperrt oder nicht lesbar: " + ex.Message); }
    }

    private static IEnumerable<string> RemoveProgram(SqliteAdministrationStore store, StudentRecord record)
    {
        var delivery = store.LatestProgramDelivery(record.Student.ExternalStudentId);
        var root = Path.Combine(record.HomeDirectory, ProgramFolder);
        if (!Directory.Exists(root)) return [];
        var kept = new List<string>();
        var delivered = delivery?.Files.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (delivered.TryGetValue(relative, out var hash) && new FileInfo(file).LinkTarget is null && Token(File.ReadAllBytes(file)) == hash) File.Delete(file);
            else kept.Add(ProgramFolder + "/" + relative);
        }
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).Append(root))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        return kept;
    }

    public static string Token(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
