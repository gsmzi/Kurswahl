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
            store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), id, "Demo Test", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
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
        Assert.Single(service.Preview([External("DEMO-001")], Origin).Items, i => i.Kind == ReconciliationKind.InvalidIdentity);
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
    public void BackupKeepsChoiceHistory()
    {
        using var store = Seed();
        var active = store.History("demo-001").Single();
        new TeacherSession(store, catalog, "demo-test").Save(active.Snapshot, active.Id, RevisionSource.TeacherEdit, "Nach Beratung geändert");
        var archive = Path.Combine(root, "backup.gsm-transfer");
        WorkspaceTransfer.Export(new TeacherWorkspace(root).Active!, store, archive);
        var restored = WorkspaceTransfer.Import(archive, Path.Combine(root, "restore"));
        using var check = restored.OpenActive(); Assert.Equal(2, check.History("demo-001").Length);
    }

    [Fact]
    public void AsvComparisonMatchesBilingualBiologyWithNoteAndDetectsTypeOralWarningAndDepartureDifferences()
    {
        var biliCatalog = catalog with
        {
            Offers = [.. catalog.Offers, new("BIO-LF-BILI", "BIO", CourseType.Lf, CourseVariant.Bilingual, OfferStatus.Offered,
                [new(1, 5), new(2, 5), new(3, 5), new(4, 5)], [Requirement.ContinuousBilingual])]
        };
        var workspace = new TeacherWorkspace(root); workspace.Create(biliCatalog, "demo-test");
        using var store = workspace.OpenActive();
        store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
        store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-002", "Demo Zwei", "10a", 2029, DepartureStatus.ConfirmedDeparture, PriorEducation.Unknown), "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);

        var session = new TeacherSession(store, biliCatalog, "demo-test");
        session.Save(TestData.Choice with
        {
            StudentId = "demo-001",
            Selections = [TestData.Select(biliCatalog.Offers.Single(o => o.Id == "BIO-LF-BILI")), TestData.Select(biliCatalog.Offers.Single(o => o.Id == "D-LF"))],
            PlannedOralSubjects = ["M", "D"]
        }, null, RevisionSource.TeacherEdit, "Ursprung");

        var service = new ChoiceReconciliation(store, biliCatalog, "demo-test");

        // 1. BIO-LF-BILI internally matches normal BIO-LF in ASV + reversed oral order is identical + missing identity reported + ConfirmedDeparture detail
        var extMatch = new ExternalChoice("demo-001", 2029,
            [TestData.Select(biliCatalog.Offers.Single(o => o.Id == "BIO-LF")), TestData.Select(biliCatalog.Offers.Single(o => o.Id == "D-LF"))])
        {
            CompareOralSubjects = true,
            IgnoreBilingualVariant = true,
            PlannedOralSubjects = ["D", "M"]
        };
        var previewMatch = service.Preview([extMatch], Origin, [(4, "Fehlende Id", "KS1")]);
        var itemMatch = previewMatch.Items.Single(i => i.StudentId == "demo-001");
        Assert.Equal(ReconciliationKind.Identical, itemMatch.Kind);
        Assert.Empty(itemMatch.Differences);
        Assert.Contains(itemMatch.Notes, n => n.Contains("bilingual", StringComparison.OrdinalIgnoreCase));

        var itemInternal = previewMatch.Items.Single(i => i.StudentId == "demo-002");
        Assert.Equal(ReconciliationKind.OnlyInternal, itemInternal.Kind);
        Assert.Contains("bestätigter Abgänger", itemInternal.Detail);

        var itemMissingId = previewMatch.Items.Single(i => i.Kind == ReconciliationKind.InvalidIdentity);
        Assert.Contains("Zeile 4", itemMissingId.Detail);

        // 2. BIO-LF-BILI internally vs BIO-BF in ASV + different oral subjects + unknown ASV warning => Changed with clear lines
        var extMismatch = new ExternalChoice("demo-001", 2029,
            [TestData.Select(biliCatalog.Offers.Single(o => o.Id == "BIO-BF")), TestData.Select(biliCatalog.Offers.Single(o => o.Id == "D-LF"))])
        {
            CompareOralSubjects = true,
            IgnoreBilingualVariant = true,
            PlannedOralSubjects = ["D", "G"],
            Warnings = ["Unbekanntes ASV-Fach im Katalog: SEM [3] (HJ 1,2)"]
        };
        var previewMismatch = service.Preview([extMismatch], Origin);
        var itemMismatch = previewMismatch.Items.Single(i => i.StudentId == "demo-001");
        Assert.Equal(ReconciliationKind.Changed, itemMismatch.Kind);
        Assert.Contains(itemMismatch.Differences, d => d.Contains("BIO", StringComparison.OrdinalIgnoreCase) && d.Contains("LF bilingual") && d.Contains("ASV BF"));
        Assert.Contains(itemMismatch.Differences, d => d.StartsWith("Mündliche Prüfungsfächer:", StringComparison.Ordinal));
        Assert.Contains(itemMismatch.Differences, d => d.Contains("SEM [3]"));

        var changedOral = extMatch with { PlannedOralSubjects = ["G", "M"] };
        var confirmed = service.Confirm(service.Preview([changedOral], Origin), "demo-001", true, "Mündliche Planung abgeglichen");
        Assert.Equal(new[] { "G", "M" }, confirmed.Snapshot.PlannedOralSubjects);
        Assert.Contains(confirmed.Snapshot.Selections, s => s.OfferId == "BIO-LF-BILI" && s.Variant == CourseVariant.Bilingual);
    }
}
