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
        if (settings.SchemaVersion is not (1 or 2) || (settings.SchemaVersion == 1 && !settings.DemoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            || (settings.SchemaVersion == 2 && settings.DemoStudentId.Length != 0))
            throw new ArgumentException("Konfigurationsschema 1 benötigt eine demo-ID; Schema 2 ermittelt die ID aus dem persönlichen Profil.");
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
        if (!(SchemaVersion == 2 && ChoiceDirectory.Length == 0) && (string.IsNullOrWhiteSpace(ChoiceDirectory) || Path.IsPathRooted(ChoiceDirectory)
            || ChoiceDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || ChoiceDirectory.Split(['/', '\\']).Any(s => string.IsNullOrWhiteSpace(s) || s.Trim() is ".." or "." || s != s.Trim() || s.EndsWith('.'))))
            throw new ArgumentException("ChoiceDirectory muss ein relativer Unterordner ohne Traversierung sein.");
        var stem = Path.GetFileNameWithoutExtension(ChoiceFileName);
        if (string.IsNullOrWhiteSpace(ChoiceFileName) || string.IsNullOrWhiteSpace(stem) || stem != stem.Trim()
            || ChoiceFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || ChoiceFileName.Contains('/') || ChoiceFileName.Contains('\\') || !ChoiceFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || ChoiceFileName.Equals("profil.json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ungültiger Wahldateiname; Profildateien sind geschützt.");
        return Path.GetFullPath(Path.Combine(home, ChoiceDirectory, ChoiceFileName));
    }

    public string ResolveCatalogPath(string settingsFile) => SchemaVersion == 2 && CatalogPath == "{WAHLORDNER}/fachangebot.json"
        ? Path.Combine(Path.GetDirectoryName(ResolveChoicePath())!, "fachangebot.json")
        : Path.GetFullPath(CatalogPath, Path.GetDirectoryName(Path.GetFullPath(settingsFile))!);

    public (string StudentId, ProfileContext Profile) ReadPersonalProfile(int graduationYear)
    {
        var directory = Path.GetDirectoryName(ResolveChoicePath())!;
        if (SchemaVersion == 1) return (DemoStudentId, new SignedProfileReader().Read(directory, DemoStudentId, graduationYear));
        return new SignedProfileReader().ReadPersonal(directory, graduationYear);
    }
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
