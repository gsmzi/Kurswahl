using System.IO.Compression;
using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class WorkspaceTransferTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-transfer-test-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private string SeedExport()
    {
        var workspace = new TeacherWorkspace(Path.Combine(root, "source")); workspace.Create(TestData.WorkflowCatalog, "demo-transfer");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using var store = workspace.OpenActive();
        store.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Transfer", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-transfer", DateTimeOffset.UtcNow, null), null);
        var session = new TeacherSession(store, TestData.WorkflowCatalog, "demo-transfer");
        session.Save(TestData.Choice, null, RevisionSource.TeacherEdit, "Demo Ursprung");
        store.EnablePlanning(); store.SavePlanningDocument(CoursePlanning.Capture(store, TestData.WorkflowCatalog, "demo-transfer", "Demo Stand", true));
        store.SetReselection("demo-001", true, null, "Demo Nachwahl");
        var file = Path.Combine(root, "transfer.gsm-transfer"); WorkspaceTransfer.Export(workspace.Active!, store, file); return file;
    }
    [Fact]
    public void RoundTripPreservesHistoryAndPlansWithoutOverwritingOldWorkspace()
    {
        var file = SeedExport(); var target = WorkspaceTransfer.Import(file, Path.Combine(root, "target"));
        Assert.Equal(YearSetupStep.Complete, target.Active!.Step);
        using (var store = target.OpenActive())
        {
            var history = store.History("demo-001"); Assert.Single(history); Assert.Single(store.PlanningDocuments());
            Assert.Equal("Demo Nachwahl", store.Reselection("demo-001").Note);
            new TeacherSession(store, target.Active.Catalog, "demo-target").Save(history[0].Snapshot, history[0].Id, RevisionSource.TeacherEdit, "Demo Rückweg");
            WorkspaceTransfer.Export(target.Active, store, Path.Combine(root, "return.gsm-transfer"));
        }
        var returned = WorkspaceTransfer.Import(Path.Combine(root, "return.gsm-transfer"), Path.Combine(root, "returned"));
        using var result = returned.OpenActive(); Assert.Equal(2, result.History("demo-001").Length);
        using var original = new TeacherWorkspace(Path.Combine(root, "source")).OpenActive(); Assert.Single(original.History("demo-001"));
        Assert.Throws<IOException>(() => WorkspaceTransfer.Import(file, target.Root));
        Assert.Throws<IOException>(() => WorkspaceTransfer.Export(target.Active, result, file));
    }
    [Fact]
    public void ChangedPreviewAndUnexpectedArchiveEntriesAreRejected()
    {
        var file = SeedExport(); var manifest = WorkspaceTransfer.Inspect(file);
        Assert.Throws<IOException>(() => WorkspaceTransfer.Import(file, Path.Combine(root, "changed"), manifest with { CreatedAt = manifest.CreatedAt.AddMinutes(-1) }));
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update)) zip.CreateEntry("../unexpected.txt");
        Assert.Throws<InvalidDataException>(() => WorkspaceTransfer.Import(file, Path.Combine(root, "unexpected")));
        Assert.False(Directory.Exists(Path.Combine(root, "unexpected")));
    }
    [Fact]
    public void CorruptDatabaseAndUnknownVersionAreRejectedBeforeImport()
    {
        var file = SeedExport();
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            zip.GetEntry("verwaltung.db")!.Delete(); using var stream = zip.CreateEntry("verwaltung.db").Open(); stream.Write([1, 2, 3]);
        }
        Assert.Throws<InvalidDataException>(() => WorkspaceTransfer.Import(file, Path.Combine(root, "invalid")));
        Assert.False(Directory.Exists(Path.Combine(root, "invalid")));
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("transfer.json")!; TransferManifest value;
            using (var stream = entry.Open()) value = JsonSerializer.Deserialize<TransferManifest>(stream, DocumentJson.Options)!;
            entry.Delete(); using var output = zip.CreateEntry("transfer.json").Open(); JsonSerializer.Serialize(output, value with { SchemaVersion = 99 }, DocumentJson.Options);
        }
        Assert.Throws<InvalidDataException>(() => WorkspaceTransfer.Inspect(file));
    }
}
