using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Storage;

public sealed record StudentSettings(int SchemaVersion, string HomeDirectory, string ChoiceDirectory,
    string ChoiceFileName, string CatalogPath, string DemoStudentId)
{
    public static StudentSettings Load(string file)
    {
        var settings = JsonSerializer.Deserialize<StudentSettings>(File.ReadAllBytes(file), JsonDocuments.Options)
            ?? throw new JsonException("Leere Anwendungskonfiguration.");
        if (settings.SchemaVersion != 1 || string.IsNullOrWhiteSpace(settings.DemoStudentId)
            || !settings.DemoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            throw new ArgumentException("M1 benötigt Schema 1 und eine synthetische demo-Schüler-ID.");
        return settings;
    }

    public string ResolveChoicePath()
    {
        var home = HomeDirectory == "{HOME}" ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : HomeDirectory;
        if (string.IsNullOrWhiteSpace(home) || !Path.IsPathFullyQualified(home))
            throw new ArgumentException("HomeDirectory muss {HOME} oder ein absoluter Pfad sein.");
        if (string.IsNullOrWhiteSpace(ChoiceDirectory) || Path.IsPathRooted(ChoiceDirectory)
            || ChoiceDirectory.Split(['/', '\\']).Any(s => s is ".." or "." or ""))
            throw new ArgumentException("ChoiceDirectory muss ein relativer Unterordner ohne Traversierung sein.");
        if (string.IsNullOrWhiteSpace(ChoiceFileName) || ChoiceFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || ChoiceFileName.Contains('/') || ChoiceFileName.Contains('\\') || !ChoiceFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || ChoiceFileName.Equals("profil.json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ungültiger Wahldateiname; Profildateien sind geschützt.");
        return Path.GetFullPath(Path.Combine(home, ChoiceDirectory, ChoiceFileName));
    }

    public string ResolveCatalogPath(string settingsFile) => Path.GetFullPath(CatalogPath, Path.GetDirectoryName(Path.GetFullPath(settingsFile))!);
}

// OS account is informative only. School account/profile binding is deliberately not claimed in M1.
public sealed class OperatingSystemIdentityProvider : ICurrentIdentityProvider
{
    public CurrentIdentity GetCurrent() => new(Environment.UserDomainName + "\\" + Environment.UserName, null);
}
