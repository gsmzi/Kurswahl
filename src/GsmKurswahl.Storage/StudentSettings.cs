using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

public sealed record StudentSettings(int SchemaVersion, string HomeDirectory, string ChoiceDirectory,
    string ChoiceFileName, string CatalogPath, string DemoStudentId)
{
    public static StudentSettings Load(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file))
            throw new ArgumentException("Konfigurationspfad muss absolut sein.");
        var settings = JsonSerializer.Deserialize<StudentSettings>(File.ReadAllBytes(file), JsonDocuments.Options)
            ?? throw new JsonException("Leere Anwendungskonfiguration.");
        if (settings.SchemaVersion != 1 || string.IsNullOrWhiteSpace(settings.DemoStudentId)
            || !settings.DemoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            throw new ArgumentException("Der Demonstrator benötigt Schema 1 und eine synthetische demo-Schüler-ID.");
        return settings;
    }

    public string ResolveHomeDirectory()
    {
        var home = HomeDirectory == "{HOME}" ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : HomeDirectory;
        if (string.IsNullOrWhiteSpace(home) || !Path.IsPathFullyQualified(home))
            throw new ArgumentException("HomeDirectory muss {HOME} oder ein absoluter Pfad sein.");
        return Path.GetFullPath(home);
    }

    public string ResolveChoicePath()
    {
        var home = ResolveHomeDirectory();
        if (string.IsNullOrWhiteSpace(ChoiceDirectory) || Path.IsPathRooted(ChoiceDirectory)
            || ChoiceDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || ChoiceDirectory.Split(['/', '\\']).Any(s => string.IsNullOrWhiteSpace(s) || s.Trim() is ".." or "." || s != s.Trim() || s.EndsWith('.')))
            throw new ArgumentException("ChoiceDirectory muss ein relativer Unterordner ohne Traversierung sein.");
        var stem = Path.GetFileNameWithoutExtension(ChoiceFileName);
        if (string.IsNullOrWhiteSpace(ChoiceFileName) || string.IsNullOrWhiteSpace(stem) || stem != stem.Trim()
            || ChoiceFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || ChoiceFileName.Contains('/') || ChoiceFileName.Contains('\\') || !ChoiceFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || ChoiceFileName.Equals("profil.json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ungültiger Wahldateiname; Profildateien sind geschützt.");
        return Path.GetFullPath(Path.Combine(home, ChoiceDirectory, ChoiceFileName));
    }

    public string ResolveCatalogPath(string settingsFile) => Path.GetFullPath(CatalogPath, Path.GetDirectoryName(Path.GetFullPath(settingsFile))!);
}

// OS identity alone grants nothing; SignedProfileReader verifies it against signed administrative binding.
public sealed class OperatingSystemIdentityProvider : ICurrentIdentityProvider
{
    public CurrentIdentity GetCurrent()
    {
        if (!OperatingSystem.IsWindows()) return new(Environment.UserName, null);
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new(identity.Name, identity.User?.Value);
    }
}
