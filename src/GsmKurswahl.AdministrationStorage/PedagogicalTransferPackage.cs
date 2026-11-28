using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed record PedagogicalPackageResult(
    string ArchivePath,
    string TransferEntryName,
    int TeacherFileCount,
    int StudentFileCount,
    bool TeacherSelfContained,
    bool StudentIncluded);

/// <summary>
/// Exports a runnable local package (.zip) for distributing and collecting choices in the pedagogical network.
/// Keeps the inner .gsm-transfer v3 archive unchanged and avoids copying local runtime databases or locks.
/// </summary>
public static class PedagogicalTransferPackage
{
    public const string StarterFileName = "Lehrerprogramm starten.cmd";
    public const string ReadmeFileName = "LIESMICH-Paedagogisches-Netz.txt";
    public const string TransferFolderName = "Transfer";
    public const string WorkspaceFolderName = "Arbeitsstand";
    public const string TeacherFolderName = "TeacherApp";
    public const string StudentFolderName = "StudentApp";
    public const string LeaseFileContent = "GSM-Kurswahl: Startsperre des Lehrerprogramms. Diese Datei im Programmordner belassen.\r\n";

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".db", ".db-wal", ".db-shm", ".gsm-transfer", ".lock", ".tmp", ".bak"
    };

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace.json", "location.json", "aktuell.json", "profil.json"
    };

    private static readonly string[] ExcludedTopFolders =
    [
        WorkspaceFolderName, "Nachwahlboegen", "Netzwechsel"
    ];

    public static string? FindSingleTransferFile(string packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory)) return null;
        var transferDir = Path.Combine(Path.GetFullPath(packageDirectory), TransferFolderName);
        if (!Directory.Exists(transferDir)) return null;
        var matches = Directory.GetFiles(transferDir, "*.gsm-transfer", SearchOption.TopDirectoryOnly);
        return matches.Length == 1 ? matches[0] : null;
    }

    public static PedagogicalPackageResult Export(
        TeacherYear year,
        SqliteAdministrationStore store,
        string destinationZip,
        string teacherProgramDirectory,
        string? studentProgramDirectory = null,
        Action<int, int, string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(destinationZip))
            throw new ArgumentException("Bitte einen Zielpfad für das Paket (.zip) angeben.", nameof(destinationZip));
        if (string.IsNullOrWhiteSpace(teacherProgramDirectory))
            throw new ArgumentException("Programmordner des Lehrerprogramms fehlt.", nameof(teacherProgramDirectory));

        var archivePath = Path.GetFullPath(destinationZip);
        var teacherDir = Path.GetFullPath(teacherProgramDirectory);
        if (!Directory.Exists(teacherDir) || !File.Exists(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.exe")))
            throw new InvalidOperationException("Der Programmordner des Lehrerprogramms enthält keine GsmKurswahl.TeacherApp.exe.");

        var resolvedStudentDir = ResolveStudentDirectory(teacherDir, studentProgramDirectory);
        if (IsSameOrInside(archivePath, teacherDir))
            throw new InvalidOperationException("Das ZIP-Paket darf nicht innerhalb des Lehrerprogramm-Ordners gespeichert werden.");
        if (resolvedStudentDir is not null && IsSameOrInside(archivePath, resolvedStudentDir))
            throw new InvalidOperationException("Das ZIP-Paket darf nicht innerhalb des Schülerprogramm-Ordners gespeichert werden.");
        if (File.Exists(archivePath))
            throw new IOException("Zieldatei existiert bereits. Bitte einen neuen Dateinamen wählen.");

        var teacherSelfContained = File.Exists(Path.Combine(teacherDir, "hostfxr.dll"))
            && BundlesRuntime(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.runtimeconfig.json"));

        var teacherFiles = CollectProgramFiles(teacherDir, skipLeaseFile: true);
        var studentFiles = resolvedStudentDir is null ? [] : CollectProgramFiles(resolvedStudentDir, skipLeaseFile: false);

        var transferFileName = $"Abitur-{year.Catalog.GraduationYear}-{DateTime.Now:yyyyMMdd-HHmmss}.gsm-transfer";
        var transferEntryName = $"{TransferFolderName}/{transferFileName}";

        var tempTransfer = Path.Combine(Path.GetTempPath(), "gsm-pkg-transfer-" + Guid.NewGuid().ToString("N") + ".gsm-transfer");
        var destinationFolder = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrEmpty(destinationFolder)) Directory.CreateDirectory(destinationFolder);
        var tempZip = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        var totalSteps = 2 + teacherFiles.Length + studentFiles.Length;
        var step = 0;

        try
        {
            progress?.Invoke(step++, totalSteps, "Transportstand (.gsm-transfer) wird erstellt …");
            WorkspaceTransfer.Export(year, store, tempTransfer);

            using (var stream = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    AddFile(zip, tempTransfer, transferEntryName, CompressionLevel.NoCompression);

                    foreach (var (relative, fullPath) in teacherFiles)
                    {
                        progress?.Invoke(step++, totalSteps, "Lehrerprogramm wird gepackt …");
                        AddFile(zip, fullPath, $"{TeacherFolderName}/{relative}", CompressionLevel.Fastest);
                    }

                    // Running instances hold an exclusive lock on the live lease file, so write a fresh marker.
                    WriteTextEntry(zip, $"{TeacherFolderName}/{TeacherProgramLease.FileName}", LeaseFileContent, Encoding.UTF8);

                    foreach (var (relative, fullPath) in studentFiles)
                    {
                        progress?.Invoke(step++, totalSteps, "Schülerprogramm wird gepackt …");
                        AddFile(zip, fullPath, $"{StudentFolderName}/{relative}", CompressionLevel.Fastest);
                    }

                    var starterContent = "@echo off\r\nsetlocal\r\nstart \"\" \"%~dp0TeacherApp\\GsmKurswahl.TeacherApp.exe\" --paket-ordner \"%~dp0.\"\r\n";
                    WriteTextEntry(zip, StarterFileName, starterContent, Encoding.ASCII);

                    var readmeContent = BuildReadme(year.Catalog.GraduationYear, transferEntryName, teacherSelfContained, resolvedStudentDir is not null);
                    WriteTextEntry(zip, ReadmeFileName, readmeContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                }
                stream.Flush(true);
            }

            File.Move(tempZip, archivePath, overwrite: false);
            progress?.Invoke(totalSteps, totalSteps, "Paket abgeschlossen");
        }
        finally
        {
            if (File.Exists(tempTransfer)) File.Delete(tempTransfer);
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }

        return new PedagogicalPackageResult(
            archivePath,
            transferEntryName,
            teacherFiles.Length + 1,
            studentFiles.Length,
            teacherSelfContained,
            resolvedStudentDir is not null);
    }

    private static string? ResolveStudentDirectory(string teacherDir, string? configuredStudentDirectory)
    {
        if (TryValidateStudentDirectory(configuredStudentDirectory) is { } explicitDir)
            return explicitDir;
        var sibling = Path.GetFullPath(Path.Combine(teacherDir, "..", StudentFolderName));
        return TryValidateStudentDirectory(sibling);
    }

    private static string? TryValidateStudentDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;
        try
        {
            var full = Path.GetFullPath(directory.Trim());
            StudentProgramPackage.Validate(full);
            return full;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static (string Relative, string FullPath)[] CollectProgramFiles(string directory, bool skipLeaseFile)
    {
        var files = new List<(string Relative, string FullPath)>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            if (ShouldExclude(relative, skipLeaseFile)) continue;
            files.Add((relative, file));
        }
        files.Sort((a, b) => StringComparer.Ordinal.Compare(a.Relative, b.Relative));
        return files.ToArray();
    }

    private static bool ShouldExclude(string relativePath, bool skipLeaseFile)
    {
        var slash = relativePath.IndexOf('/');
        if (slash >= 0)
        {
            var topFolder = relativePath[..slash];
            if (topFolder.StartsWith('.') || topFolder.StartsWith("Abitur-", StringComparison.OrdinalIgnoreCase)
                || ExcludedTopFolders.Any(f => string.Equals(f, topFolder, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        var fileName = Path.GetFileName(relativePath);
        if (skipLeaseFile && string.Equals(fileName, TeacherProgramLease.FileName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (ExcludedFileNames.Contains(fileName))
            return true;
        var extension = Path.GetExtension(fileName);
        if (ExcludedExtensions.Contains(extension))
            return true;
        // Double extensions like verwaltung.db-wal or choice.json.bak
        foreach (var suffix in ExcludedExtensions)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void AddFile(ZipArchive zip, string sourcePath, string entryName, CompressionLevel level)
    {
        var entry = zip.CreateEntry(entryName, level);
        using var target = entry.Open();
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        source.CopyTo(target);
    }

    private static void WriteTextEntry(ZipArchive zip, string entryName, string content, Encoding encoding)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        using var target = entry.Open();
        var bytes = encoding.GetBytes(content);
        target.Write(bytes);
    }

    private static bool IsSameOrInside(string candidatePath, string directoryPath)
    {
        var dir = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(candidate, directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), comparison)
            || candidate.StartsWith(dir, comparison);
    }

    private static bool BundlesRuntime(string runtimeConfig)
    {
        if (!File.Exists(runtimeConfig)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfig));
            return document.RootElement.TryGetProperty("runtimeOptions", out var options)
                && options.TryGetProperty("includedFrameworks", out _);
        }
        catch (JsonException) { return false; }
    }

    private static string BuildReadme(int graduationYear, string transferEntryName, bool teacherSelfContained, bool studentIncluded)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GSM-Kurswahl · Paket für das pädagogische Netz (Abitur {graduationYear})");
        sb.AppendLine("=======================================================================");
        sb.AppendLine();
        sb.AppendLine("Dieses Paket enthält das Lehrerprogramm, den aktuellen Jahrgangsstand");
        sb.AppendLine($"({transferEntryName})" + (studentIncluded ? " und das selbstenthaltende Schülerprogramm." : "."));
        sb.AppendLine();
        sb.AppendLine("Ablauf im pädagogischen Netz:");
        sb.AppendLine("1. Dieses ZIP-Paket vollständig in einen eigenen Ordner im persönlichen Laufwerk U: entpacken,");
        sb.AppendLine("   z. B. U:\\Kurswahl-Verteilung (nicht direkt aus dem ZIP starten). Lokale Ordner der Stationen");
        sb.AppendLine("   eignen sich nicht, weil die Selbstheilung sie beim Neustart zurücksetzt. Nur ein Lehrerprogramm");
        sb.AppendLine("   gleichzeitig öffnen; die Startsperre verhindert einen zweiten Start.");
        sb.AppendLine($"2. \"{StarterFileName}\" starten.");
        sb.AppendLine($"3. Den angezeigten Transportstand prüfen und übernehmen (Ordner \"{WorkspaceFolderName}\" entsteht automatisch im Paketordner).");
        sb.AppendLine("4. Im pädagogischen Netz das vorbelegte Grundverzeichnis der Klassenordner prüfen oder eines auswählen.");
        sb.AppendLine("   Zielordner prüfen, abweichende Namensvorschläge einzeln bestätigen und Schülerdateien verteilen.");
        sb.AppendLine("   Bei offenen Zuordnungen lässt sich ein freier Ordner derselben Klasse auswählen.");
        sb.AppendLine("   Später Kurswahlen einsammeln (nach Abgabetermin auch abschließend einsammeln und Wahlordner räumen).");
        sb.AppendLine("5. Nach dem Einsammeln unter \"Wahl -> Kurswahlen einsammeln -> Rückwechsel ins Verwaltungsnetz\"");
        sb.AppendLine("   über \"Stand für Verwaltungsnetz speichern ...\" eine .gsm-transfer-Datei erzeugen und im Verwaltungsnetz öffnen.");
        sb.AppendLine("6. WICHTIG - AUFRÄUMEN IM PÄDAGOGISCHEN NETZ:");
        sb.AppendLine("   Sobald die Rück-Transportdatei sicher im Verwaltungsnetz angekommen und dort erfolgreich geöffnet ist:");
        sb.AppendLine($"   - Lehrerprogramm im pädagogischen Netz schließen,");
        sb.AppendLine($"   - den gesamten entpackten Paketordner (samt {TeacherFolderName}, {TransferFolderName}" + (studentIncluded ? $", {StudentFolderName}" : "") + $" und {WorkspaceFolderName})");
        sb.AppendLine("     sowie die übertragene ZIP-Datei im pädagogischen Netz vollständig löschen.");
        if (!studentIncluded || !teacherSelfContained)
        {
            sb.AppendLine();
            sb.AppendLine("Technische Hinweise zu diesem Paket:");
            if (!studentIncluded)
            {
                sb.AppendLine("- Es war kein selbstenthaltendes Schülerprogramm (StudentApp) konfiguriert bzw. neben dem Lehrerprogramm vorhanden.");
                sb.AppendLine("  Falls nötig, unter \"3 · Schülerdateien verteilen\" bzw. Einstellungen den Pfad zu StudentApp wählen.");
            }
            if (!teacherSelfContained)
            {
                sb.AppendLine("- Diese Lehrerprogramm-Installation benötigt auf dem Zielrechner eine installierte .NET-10-Desktop-Laufzeit.");
                sb.AppendLine("  Für eine laufzeitunabhängige Version im Verwaltungsnetz das selbstenthaltende Release-Paket verwenden.");
            }
        }
        return sb.ToString();
    }
}
