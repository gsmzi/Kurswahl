using GsmKurswahl.AdministrationStorage;

namespace GsmKurswahl.Storage.Tests;

public sealed class SchoolDefaultsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-defaults-" + Guid.NewGuid());
    public SchoolDefaultsTests() => Directory.CreateDirectory(root);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    // Minimal shape of a self-contained publish output (synthetic content).
    internal static void SelfContainedStub(string program)
    {
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.exe"), "exe");
        File.WriteAllText(Path.Combine(program, "hostfxr.dll"), "host");
        File.WriteAllText(Path.Combine(program, "GsmKurswahl.StudentApp.runtimeconfig.json"),
            """{ "runtimeOptions": { "tfm": "net10.0", "includedFrameworks": [ { "name": "Microsoft.NETCore.App", "version": "10.0.14" } ] } }""");
    }

    [Fact] public void MissingFileMeansNoPresets() => Assert.Equal(SchoolDefaults.None, SchoolDefaults.Load(root));

    [WindowsFact] public void CheckedInGsmPresetsAreValid()
    {
        var teacher = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GsmKurswahl.TeacherApp"));
        var defaults = SchoolDefaults.Load(teacher);
        Assert.True(Path.IsPathFullyQualified(defaults.DistributionRoot));
        Assert.Equal(Path.GetFullPath(Path.Combine(teacher, "..", "StudentApp")), defaults.StudentProgramDirectory);
    }

    [WindowsFact] public void RelativeProgramFolderIsResolvedNextToTeacherProgram()
    {
        var teacher = Path.Combine(root, "TeacherApp"); Directory.CreateDirectory(teacher);
        File.WriteAllText(Path.Combine(teacher, SchoolDefaults.FileName),
            """{ "schemaVersion": 1, "distributionRoot": "X:\\Klassen", "studentProgramDirectory": "..\\StudentApp" }""");
        var defaults = SchoolDefaults.Load(teacher);
        Assert.Equal("X:\\Klassen", defaults.DistributionRoot);
        Assert.Equal(Path.Combine(root, "StudentApp"), defaults.StudentProgramDirectory);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "distributionRoot": "", "studentProgramDirectory": "" }""")]
    [InlineData("""{ "schemaVersion": 1, "distributionRoot": "Klassen", "studentProgramDirectory": "" }""")]
    public void UnknownVersionOrRelativeRootIsRejectedAndKept(string json)
    {
        var path = Path.Combine(root, SchoolDefaults.FileName); File.WriteAllText(path, json);
        Assert.Throws<InvalidDataException>(() => SchoolDefaults.Load(root));
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact] public void FrameworkDependentProgramIsRejected()
    {
        File.WriteAllText(Path.Combine(root, "GsmKurswahl.StudentApp.exe"), "exe");
        File.WriteAllText(Path.Combine(root, "GsmKurswahl.StudentApp.runtimeconfig.json"),
            """{ "runtimeOptions": { "tfm": "net10.0", "framework": { "name": "Microsoft.NETCore.App", "version": "10.0.0" } } }""");
        var error = Assert.Throws<ArgumentException>(() => StudentProgramPackage.Validate(root));
        Assert.Contains(".NET", error.Message);
        SelfContainedStub(root);
        StudentProgramPackage.Validate(root);
    }
}
