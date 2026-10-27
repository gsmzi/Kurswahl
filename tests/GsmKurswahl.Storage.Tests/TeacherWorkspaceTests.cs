using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class TeacherWorkspaceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-workspace-" + Guid.NewGuid());
    [Fact]
    public void YearAndSetupResumeAndExplicitActivationSurviveRestart()
    {
        var workspace = new TeacherWorkspace(root); Assert.Null(workspace.Active);
        workspace.Create(TestData.Catalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Mapping, CertificateThumbprint = "synthetic-thumbprint", KeyId = "demo-key" });
        var reopened = new TeacherWorkspace(root);
        Assert.Equal(YearSetupStep.Mapping, reopened.Active!.Step); Assert.Equal("demo-key", reopened.Active.KeyId);
        reopened.Create(TestData.Catalog with { GraduationYear = 2030, Version = "demo-2030-v1" }, "demo-beratung");
        reopened.Activate(2029);
        Assert.Equal(2029, new TeacherWorkspace(root).Active!.Catalog.GraduationYear);
        Assert.Equal(2, new TeacherWorkspace(root).Document.Years.Length);
        using var db = reopened.OpenActive(); Assert.Empty(db.Students());
    }
    [Fact]
    public void MissingRegisteredDatabaseIsNeverRecreated()
    {
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.Catalog, "demo-beratung");
        var database = workspace.Active!.DatabasePath; File.Delete(database);
        Assert.Throws<FileNotFoundException>(() => new TeacherWorkspace(root).OpenActive());
        Assert.False(File.Exists(database));
    }
    [Fact]
    public void StaleWorkspaceWriteIsRejectedAndCurrentStatePreserved()
    {
        var first = new TeacherWorkspace(root); first.Create(TestData.Catalog, "demo-beratung");
        var second = new TeacherWorkspace(root);
        first.Update(first.Active! with { Step = YearSetupStep.Import });
        Assert.Throws<IOException>(() => second.Update(second.Active! with { Step = YearSetupStep.Complete }));
        Assert.Equal(YearSetupStep.Import, new TeacherWorkspace(root).Active!.Step);
    }
    [Theory]
    [InlineData("broken")]
    [InlineData("{\"schemaVersion\":99,\"activeYear\":null,\"years\":[]}")]
    public void InvalidWorkspaceIsNotOverwritten(string text)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "workspace.json"); File.WriteAllText(path, text);
        Assert.ThrowsAny<Exception>(() => new TeacherWorkspace(root)); Assert.Equal(text, File.ReadAllText(path));
    }
    [Fact]
    public void ExistingDatabaseCanBeAttachedWithoutChangingItsRecords()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "previous.db");
        using (var db = new SqliteAdministrationStore(path))
            db.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10-demo", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var workspace = new TeacherWorkspace(Path.Combine(root, "managed"));
        Assert.Throws<InvalidDataException>(() => workspace.Attach(TestData.Catalog with { GraduationYear = 2030 }, path, "demo-beratung"));
        workspace.Attach(TestData.Catalog, path, "demo-beratung");
        using var attached = new TeacherWorkspace(Path.Combine(root, "managed")).OpenActive();
        Assert.Equal("Demo Eins", attached.Students().Single().Student.SyntheticDisplayName);
    }
    [Fact]
    public void LocationIsRememberedButMissingTargetDoesNotFallBackToEmptyState()
    {
        var settings = new TeacherWorkspaceLocation(root); Assert.Equal(root, settings.Load());
        var other = Path.Combine(root, "other"); Directory.CreateDirectory(other); settings.Remember(other);
        Assert.Equal(other, new TeacherWorkspaceLocation(root).Load());
        Directory.Delete(other); Assert.Throws<DirectoryNotFoundException>(() => settings.Load());
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
