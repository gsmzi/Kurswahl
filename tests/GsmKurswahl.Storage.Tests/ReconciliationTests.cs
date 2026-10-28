using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class ReconciliationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-reconcile-" + Guid.NewGuid());
    private readonly OfferCatalog catalog = TestData.WorkflowCatalog;
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private SqliteAdministrationStore Seed()
    {
        var workspace = new TeacherWorkspace(root); workspace.Create(catalog, "demo-test");
        var store = workspace.OpenActive();
        foreach (var id in new[] { "demo-001", "demo-002" })
            store.SaveStudent(new(1, new(Guid.NewGuid(), id, "Demo Test", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
        new TeacherSession(store, catalog, "demo-test").Save(TestData.Choice, null, RevisionSource.TeacherEdit, "Ursprung");
        return store;
    }
    private ExternalChoice External(string id = "demo-001") => new(id, 2029, [TestData.Select(catalog.Offers.Single(o => o.Id == "D-LF"))]);
    private static ImportOrigin Origin => new("synthetic.csv", "synthetic-hash", "mapping-v1", DateTimeOffset.UtcNow, "");

    [Fact]
    public void PreviewIsReadOnlyAndSeparatesNewMissingChangedAndDuplicateIdentities()
    {
        using var store = Seed(); var service = new ChoiceReconciliation(store, catalog, "demo-test");
        var preview = service.Preview([External(), External("demo-003")], Origin);
        Assert.Equal(ReconciliationKind.Changed, preview.Items.Single(i => i.StudentId == "demo-001").Kind);
        Assert.Equal(ReconciliationKind.OnlyInternal, preview.Items.Single(i => i.StudentId == "demo-002").Kind);
        Assert.Equal(ReconciliationKind.OnlyExternal, preview.Items.Single(i => i.StudentId == "demo-003").Kind);
        Assert.Single(store.History("demo-001")); Assert.Equal(2, store.Students().Length);
        Assert.Throws<InvalidOperationException>(() => service.Confirm(preview, "demo-001", false, "Test"));
        Assert.Throws<InvalidOperationException>(() => service.Confirm(preview, "demo-003", true, "Test"));
        Assert.Contains(service.Preview([External(), External()], Origin).Items, i => i.Kind == ReconciliationKind.InvalidIdentity);
    }
    [Fact]
    public void ConfirmAppendsOriginAndRepeatedImportIsIdentical()
    {
        using var store = Seed(); var service = new ChoiceReconciliation(store, catalog, "demo-test");
        var preview = service.Preview([External()], Origin);
        Assert.NotEqual(ReviewStatus.Valid, preview.Items[0].Validation!.Status);
        var revision = service.Confirm(preview, "demo-001", true, "Geprüfter Abgleich");
        Assert.Equal(RevisionSource.AsvReconciliation, revision.Source); Assert.Equal(Origin.SourceId, revision.ImportOrigin!.SourceId);
        Assert.Equal(2, store.History("demo-001").Length); Assert.Empty(store.History("demo-001")[0].Snapshot.Selections);
        Assert.Equal(SubmissionStatus.Draft, revision.Snapshot.SubmissionStatus);
        var again = service.Preview([External()], Origin);
        Assert.Equal(ReconciliationKind.Identical, again.Items.Single(i => i.StudentId == "demo-001").Kind);
        Assert.Throws<InvalidOperationException>(() => service.Confirm(again, "demo-001", true, "Wiederholung"));
        Assert.Throws<InvalidOperationException>(() => service.Confirm(preview, "demo-001", true, "Veraltet"));
    }
    [Fact]
    public void ChangedPrerequisitesOrCatalogInvalidatePreview()
    {
        using var store = Seed(); var service = new ChoiceReconciliation(store, catalog, "demo-test");
        var preview = service.Preview([External()], Origin);
        Assert.Throws<InvalidOperationException>(() => new ChoiceReconciliation(store, catalog with { Version = "demo-new" }, "demo-test").Confirm(preview, "demo-001", true, "Test"));
        var record = store.Students()[0]; store.SaveStudent(record with { Version = 2, Student = record.Student with { Prerequisites = record.Student.Prerequisites with { InformaticsImp = Knowledge.Yes } } }, 1);
        Assert.Throws<InvalidOperationException>(() => service.Confirm(preview, "demo-001", true, "Test"));
    }
    [Fact]
    public void ReceiptsRequireExplicitMigrationAndSurviveBackupWithHistory()
    {
        using var store = Seed();
        var active = store.History("demo-001").Single();
        var receipt = new TransferReceipt(1, Guid.NewGuid(), "demo-001", active.Id, 1, catalog.Version, active.RuleVersion, DateTimeOffset.UtcNow, "demo-test", true, "Synthetische manuelle Eingabe");
        Assert.Throws<InvalidOperationException>(() => store.RecordTransfer(receipt));
        store.EnablePlanning(); store.EnableTransferTracking(); Assert.True(store.PlanningEnabled);
        store.RecordTransfer(receipt); store.RecordTransfer(receipt with { Id = Guid.NewGuid(), Entered = false, Note = "Erneut prüfen" });
        Assert.Equal(2, store.TransferReceipts().Length);
        new TeacherSession(store, catalog, "demo-test").Save(active.Snapshot, active.Id, RevisionSource.TeacherEdit, "Nach Vermerk geändert");
        Assert.Throws<InvalidOperationException>(() => store.RecordTransfer(receipt));
        var archive = Path.Combine(root, "backup.gsm-transfer");
        WorkspaceTransfer.Export(new TeacherWorkspace(root).Active!, store, archive);
        var restored = WorkspaceTransfer.Import(archive, Path.Combine(root, "restore"));
        using var check = restored.OpenActive(); Assert.Equal(2, check.TransferReceipts().Length); Assert.Equal(2, check.History("demo-001").Length);
        Assert.True(check.TransferTrackingEnabled); Assert.True(check.PlanningEnabled);
    }
}
