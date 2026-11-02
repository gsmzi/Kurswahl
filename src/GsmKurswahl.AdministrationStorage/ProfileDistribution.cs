using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

// Profile creation and distribution in the teacher app; no certificate or SID required.
public sealed class ProfileDistribution(IAdministrationStore store)
{
    public IssuedProfile Issue(StudentRecord record)
    {
        var current = store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
        if (current.Version != record.Version) throw new InvalidOperationException("Stammdaten geändert.");
        record = current;
        if (!record.MappingConfirmed || !Path.IsPathFullyQualified(record.HomeDirectory))
            throw new ArgumentException("Bestätigte Schüler-/Ordnerzuordnung erforderlich.");
        var previous = store.LatestProfile(record.Student.ExternalStudentId);
        var profile = new StudentProfile(SchemaVersions.Profile, (previous?.Profile.ProfileRevision ?? 0) + 1, record.Student.ExternalStudentId,
            record.WindowsUsername, record.Student.GraduationYear, DateTimeOffset.UtcNow, record.Student.Prerequisites)
        { DisplayName = record.Student.SyntheticDisplayName, ClassName = record.Student.ClassName };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, DocumentJson.Options);
        if (bytes.Length > 65536) throw new ArgumentException("Profil überschreitet 64 KiB.");
        var issued = new IssuedProfile(profile, bytes, []);
        store.SaveProfile(issued);
        return issued;
    }
    public DistributionEntry Distribute(StudentRecord record, IssuedProfile issued, OfferCatalog? catalog = null)
    {
        var status = "verteilt"; var detail = "Einzelprofil verteilt; Schülerwahlen unverändert.";
        try
        {
            var current = store.Students().Single(s => s.Student.ExternalStudentId == record.Student.ExternalStudentId);
            if (current.Version != record.Version) throw new InvalidDataException("veraltet");
            record = current;
            var latest = store.LatestProfile(record.Student.ExternalStudentId);
            if (!record.MappingConfirmed || issued.Profile.StudentId != record.Student.ExternalStudentId
                || issued.Profile.GraduationYear != record.Student.GraduationYear
                || JsonSerializer.Serialize(issued.Profile.Prerequisites, DocumentJson.Options) != JsonSerializer.Serialize(record.Student.Prerequisites, DocumentJson.Options)
                || latest?.Profile.ProfileRevision != issued.Profile.ProfileRevision
                || !latest.ProfileBytes.SequenceEqual(issued.ProfileBytes))
                throw new InvalidDataException("veraltet");
            if (!Path.IsPathFullyQualified(record.HomeDirectory) || !Directory.Exists(record.HomeDirectory)) throw new DirectoryNotFoundException();
            // The home folder must exist; only our own Kurswahl/Daten below it is created.
            var data = Directory.CreateDirectory(StudentFolderLayout.Data(record.HomeDirectory)).FullName;
            using var lease = new FileStream(Path.Combine(data, ".profil.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var path = Path.Combine(data, "profil.json");
            var before = ReadExisting(path);
            if (before is not null)
            {
                var old = JsonSerializer.Deserialize<StudentProfile>(before, DocumentJson.Options);
                if (old is null || old.SchemaVersion != SchemaVersions.Profile || old.StudentId != issued.Profile.StudentId || old.GraduationYear != issued.Profile.GraduationYear)
                    throw new InvalidDataException("Profil_abweichend");
                if (old.ProfileRevision > issued.Profile.ProfileRevision) throw new InvalidDataException("veraltet");
                if (old.ProfileRevision == issued.Profile.ProfileRevision && !before.SequenceEqual(issued.ProfileBytes)) throw new InvalidDataException("Profil_abweichend");
            }
            Replace(path, issued.ProfileBytes, before);
            if (catalog is not null)
            {
                ModelGuard.Catalog(catalog);
                if (catalog.GraduationYear != record.Student.GraduationYear) throw new InvalidDataException("Profil_abweichend");
                var catalogPath = Path.Combine(data, "fachangebot.json");
                var oldCatalog = ReadExisting(catalogPath);
                if (oldCatalog is not null)
                {
                    var parsed = JsonSerializer.Deserialize<OfferCatalog>(oldCatalog, DocumentJson.Options) ?? throw new JsonException();
                    ModelGuard.Catalog(parsed);
                    if (parsed.GraduationYear != catalog.GraduationYear) throw new InvalidDataException("Profil_abweichend");
                }
                Replace(catalogPath, JsonSerializer.SerializeToUtf8Bytes(catalog, DocumentJson.Options), oldCatalog);
            }
        }
        catch (DirectoryNotFoundException) { status = "nicht_gefunden"; detail = "Bestätigtes Zielverzeichnis nicht erreichbar."; }
        catch (UnauthorizedAccessException) { status = "Zugriff_verweigert"; detail = "Keine Schreibberechtigung am Ziel."; }
        catch (InvalidDataException ex) { status = ex.Message; detail = "Profil oder Verwaltungsstand abweichend; Vorschau erneuern und Zuordnung prüfen."; }
        catch (JsonException) { status = "Profil_abweichend"; detail = "Defektes vorhandenes Profil bleibt erhalten."; }
        catch (IOException) { status = "Zugriff_verweigert"; detail = "Ziel gesperrt, verändert oder Schreibvorgang fehlgeschlagen; Wiederholung erforderlich."; }
        var entry = new DistributionEntry(Guid.NewGuid(), record.Student.ExternalStudentId, issued.Profile.ProfileRevision, DateTimeOffset.UtcNow, status, detail);
        store.LogDistribution(entry); return entry;
    }
    /// <summary>
    /// Copies the published pupil program into the confirmed pupil folder and records every file with its hash, so
    /// the final collection can later remove exactly these unchanged copies. Foreign or modified files are never replaced.
    /// </summary>
    public DistributionEntry DeliverProgram(StudentRecord record, string sourceDirectory)
    {
        var status = "verteilt"; var detail = "Schülerprogramm bereitgestellt.";
        try
        {
            if (store is not SqliteAdministrationStore sql) throw new InvalidOperationException("Programmverteilung benötigt den lokalen Lehrerbestand.");
            StudentProgramPackage.Validate(sourceDirectory);
            if (!record.MappingConfirmed || !Path.IsPathFullyQualified(record.HomeDirectory) || !Directory.Exists(record.HomeDirectory)) throw new DirectoryNotFoundException();
            var source = Path.GetFullPath(sourceDirectory);
            var target = StudentFolderLayout.Program(record.HomeDirectory);
            if (target.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Programmordner darf nicht im Schülerordner liegen.");
            var previous = sql.LatestProgramDelivery(record.Student.ExternalStudentId)?.Files.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.OrdinalIgnoreCase) ?? [];
            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                .Select(f => (Relative: Path.GetRelativePath(source, f).Replace(Path.DirectorySeparatorChar, '/'), Bytes: File.ReadAllBytes(f))).ToArray();
            // Check every target first; nothing is written if any existing file is foreign or changed.
            foreach (var (relative, bytes) in files)
            {
                var path = Path.Combine(target, relative);
                if (!File.Exists(path)) continue;
                var current = StudentFolderCleanup.Token(File.ReadAllBytes(path));
                if (current != StudentFolderCleanup.Token(bytes) && (!previous.TryGetValue(relative, out var known) || known != current))
                    throw new InvalidDataException("Programm_abweichend");
            }
            var launcher = StudentFolderLayout.Launcher(record.HomeDirectory);
            var launcherBytes = StudentFolderCleanup.LauncherBytes;
            var existingLauncher = File.Exists(launcher) ? File.ReadAllBytes(launcher) : null;
            if (existingLauncher is not null && !existingLauncher.AsSpan().SequenceEqual(launcherBytes)) throw new InvalidDataException("Programm_abweichend");
            foreach (var (relative, bytes) in files)
            {
                var path = Path.Combine(target, relative);
                if (File.Exists(path) && StudentFolderCleanup.Token(File.ReadAllBytes(path)) == StudentFolderCleanup.Token(bytes)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Replace(path, bytes, File.Exists(path) ? File.ReadAllBytes(path) : null);
            }
            foreach (var (relative, hash) in previous.Where(p => !files.Any(f => string.Equals(f.Relative, p.Key, StringComparison.OrdinalIgnoreCase))))
            {
                var path = Path.Combine(target, relative);
                if (File.Exists(path) && StudentFolderCleanup.Token(File.ReadAllBytes(path)) == hash) File.Delete(path);
            }
            if (existingLauncher is null) { Directory.CreateDirectory(Path.GetDirectoryName(launcher)!); Replace(launcher, launcherBytes, null); }
            sql.RecordProgramDelivery(new(1, Guid.NewGuid(), record.Student.ExternalStudentId, DateTimeOffset.UtcNow, StudentFolderCleanup.ProgramFolder,
                [.. files.Select(f => new DeliveredFile(f.Relative, StudentFolderCleanup.Token(f.Bytes)))]));
        }
        catch (DirectoryNotFoundException) { status = "nicht_gefunden"; detail = "Bestätigtes Zielverzeichnis nicht erreichbar."; }
        catch (UnauthorizedAccessException) { status = "Zugriff_verweigert"; detail = "Keine Schreibberechtigung am Ziel."; }
        catch (InvalidDataException ex) { status = ex.Message; detail = "Vorhandene Programmdateien weichen ab und bleiben unverändert."; }
        catch (IOException) { status = "Zugriff_verweigert"; detail = "Programmdatei gesperrt (Programm geöffnet?) oder Schreibvorgang fehlgeschlagen."; }
        return new(Guid.NewGuid(), record.Student.ExternalStudentId, 0, DateTimeOffset.UtcNow, status, detail);
    }
    private static byte[]? ReadExisting(string path)
    {
        if (!File.Exists(path)) return null;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 65536) throw new InvalidDataException("Profil_abweichend");
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
    private static void Replace(string path, byte[] bytes, byte[]? expected)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            var actual = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if ((expected is null) != (actual is null) || (expected is not null && !expected.SequenceEqual(actual!))) throw new IOException("Konkurrenzänderung.");
            if (expected is null) File.Move(temp, path); else File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
