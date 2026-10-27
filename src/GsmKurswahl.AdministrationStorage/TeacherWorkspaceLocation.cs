using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

public sealed class TeacherWorkspaceLocation(string settingsDirectory)
{
    private sealed record Location(int SchemaVersion, string Directory);
    private string PathName => Path.Combine(settingsDirectory, "location.json");
    public string Load()
    {
        if (!File.Exists(PathName)) return settingsDirectory;
        var location = JsonSerializer.Deserialize<Location>(File.ReadAllBytes(PathName), DocumentJson.Options);
        if (location is null || location.SchemaVersion != 1 || !Path.IsPathFullyQualified(location.Directory)) throw new InvalidDataException("Ungültige Speicherort-Einstellung; Datei bleibt erhalten.");
        if (!Directory.Exists(location.Directory)) throw new DirectoryNotFoundException("Gespeichertes Verwaltungsverzeichnis ist nicht erreichbar.");
        return location.Directory;
    }
    public void Remember(string root)
    {
        // Validate the current file before replacement; never silently repair malformed settings.
        var before = File.Exists(PathName) ? File.ReadAllBytes(PathName) : null;
        if (before is not null)
        {
            var existing = JsonSerializer.Deserialize<Location>(before, DocumentJson.Options);
            if (existing is null || existing.SchemaVersion != 1) throw new InvalidDataException("Unbekannte Speicherort-Einstellung.");
        }
        Directory.CreateDirectory(settingsDirectory);
        using var lease = new FileStream(PathName + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var now = File.Exists(PathName) ? File.ReadAllBytes(PathName) : null;
        if ((before is null) != (now is null) || before is not null && !before.SequenceEqual(now!)) throw new IOException("Speicherort inzwischen geändert.");
        var temp = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(JsonSerializer.SerializeToUtf8Bytes(new Location(1, Path.GetFullPath(root)), DocumentJson.Options)); output.Flush(true); }
            if (before is null) File.Move(temp, PathName); else File.Replace(temp, PathName, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
