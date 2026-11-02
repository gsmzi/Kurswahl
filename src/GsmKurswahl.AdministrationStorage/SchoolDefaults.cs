using System.Text.Json;
using GsmKurswahl.Domain;

namespace GsmKurswahl.AdministrationStorage;

/// <summary>
/// Optional school presets next to the teacher program (schulvorgaben.json): the drive/base folder with the class
/// folders and the published pupil program. Kept out of the repository because it names concrete school paths.
/// Missing file means no presets; a malformed or unknown file is reported and never rewritten.
/// </summary>
public sealed record SchoolDefaults(int SchemaVersion, string DistributionRoot, string StudentProgramDirectory)
{
    public const string FileName = "schulvorgaben.json";
    public static readonly SchoolDefaults None = new(1, "", "");

    public static SchoolDefaults Load(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path)) return None;
        var defaults = JsonSerializer.Deserialize<SchoolDefaults>(File.ReadAllBytes(path), DocumentJson.Options)
            ?? throw new InvalidDataException("Leere Schulvorgaben.");
        if (defaults.SchemaVersion != 1) throw new InvalidDataException("Unbekannte Version der Schulvorgaben; Datei bleibt unverändert.");
        var root = (defaults.DistributionRoot ?? "").Trim();
        var program = (defaults.StudentProgramDirectory ?? "").Trim();
        if (root.Length > 0 && !Path.IsPathFullyQualified(root)) throw new InvalidDataException("distributionRoot muss ein vollständiger Pfad sein.");
        // The program folder may be relative to the teacher program, e.g. ..\StudentApp inside a release package.
        if (program.Length > 0) program = Path.GetFullPath(program, Path.GetFullPath(directory));
        return defaults with { DistributionRoot = root, StudentProgramDirectory = program };
    }
}

/// <summary>Accepts only a self-contained pupil program, so pupil computers need no installed .NET runtime.</summary>
public static class StudentProgramPackage
{
    public static void Validate(string directory)
    {
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory)
            || !File.Exists(Path.Combine(directory, "GsmKurswahl.StudentApp.exe")))
            throw new ArgumentException("Bitte den vollständigen Pfad des Ordners mit GsmKurswahl.StudentApp.exe angeben.");
        if (!File.Exists(Path.Combine(directory, "hostfxr.dll")) || !BundlesRuntime(Path.Combine(directory, "GsmKurswahl.StudentApp.runtimeconfig.json")))
            throw new ArgumentException("Dieses Schülerprogramm bringt keine eigene .NET-Laufzeit mit und würde auf Schülerrechnern eine .NET-Installation verlangen. Bitte den Ordner „StudentApp“ aus dem Release-Paket verwenden.");
    }

    // A self-contained build lists "includedFrameworks"; a framework-dependent one lists "framework(s)" instead.
    private static bool BundlesRuntime(string runtimeConfig)
    {
        if (!File.Exists(runtimeConfig)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfig));
            return document.RootElement.TryGetProperty("runtimeOptions", out var options) && options.TryGetProperty("includedFrameworks", out _);
        }
        catch (JsonException) { return false; }
    }
}
