using System.Collections.Immutable;
using System.Text.Json;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

public sealed class PlanningTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gsm-planning-" + Guid.NewGuid());
    private string Database => Path.Combine(root, "verwaltung.db");
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private static OfferCatalog Catalog => TestData.WorkflowCatalog with
    {
        Offers = TestData.WorkflowCatalog.Offers.Add(TestData.Offer("BIO-LF-BILI", "BIO", CourseType.Lf) with { Variant = CourseVariant.Bilingual, Requirements = [Requirement.ContinuousBilingual] })
    };
    private static void Seed(SqliteAdministrationStore store)
    {
        for (var i = 1; i <= 3; i++)
        {
            var id = $"demo-{i:000}";
            var record = new StudentRecord(1, new(Guid.NewGuid(), id, "Demo " + i, "10a", 2029,
                i == 1 ? DepartureStatus.Active : i == 2 ? DepartureStatus.ExpectedDeparture : DepartureStatus.ConfirmedDeparture,
                PriorEducation.Unknown with { ContinuousBilingual = Knowledge.Yes }), "", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null);
            store.SaveStudent(record, null);
            var choice = TestData.Choice with { StudentId = id, Selections = [TestData.Select(Catalog.Offers.Single(o => o.Id == (i == 1 ? "BIO-LF-BILI" : "BIO-LF"))), TestData.Select(Catalog.Offers.Single(o => o.Id == "M-LF"))] };
            new TeacherSession(store, Catalog, "demo-beratung").Save(choice, null, RevisionSource.StudentSubmission, "Demo-Abgabe");
        }
    }
    private static PlanningDocument Capture(SqliteAdministrationStore store, bool include = true) => CoursePlanning.Capture(store, Catalog, "demo-beratung", "Demo Planung", include);
    [Fact]
    public void ScenarioDoesNotWriteAndCancelsOnlyExactVariant()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store);
        var before = JsonSerializer.Serialize(store.History("demo-001"), DocumentJson.Options);
        var preview = CoursePlanning.PreviewCancellation(Capture(store), ["BIO-LF-BILI"]);
        Assert.Single(preview.Changes.Where(c => !c.RemovedOffers.IsEmpty));
        Assert.DoesNotContain(preview.Changes.Single(c => c.StudentId == "demo-001").Choice.Selections, s => s.OfferId == "BIO-LF-BILI");
        Assert.Contains(preview.Changes.Single(c => c.StudentId == "demo-002").Choice.Selections, s => s.OfferId == "BIO-LF");
        Assert.Equal(before, JsonSerializer.Serialize(store.History("demo-001"), DocumentJson.Options));
        Assert.False(store.PlanningEnabled);
    }
    [Fact]
    public void DepartureFilterOnlyChangesPlanningNumbersAndGroupEstimate()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store);
        var all = CoursePlanning.Analyze(Capture(store), 1);
        var active = CoursePlanning.Analyze(Capture(store, false), 24);
        Assert.Equal(3, all.All); Assert.Equal(2, all.Relevant); Assert.Equal(1, active.Relevant);
        Assert.Equal(2, all.Demand.Single(d => d.OfferId == "M-LF" && d.Semester == 1).EstimatedGroups);
        Assert.Equal(1, active.Demand.Single(d => d.OfferId == "M-LF" && d.Semester == 1).EstimatedGroups);
        Assert.Equal(3, store.Students().Length);
        Assert.Equal(2, all.Combinations.Length); Assert.NotEmpty(all.Overlaps);
        Assert.Equal(JsonSerializer.Serialize(active), JsonSerializer.Serialize(CoursePlanning.Analyze(Capture(store, false), 24)));
    }
    [Fact]
    public void ConfirmationIsAtomicVersionedAndSurvivesReopen()
    {
        Guid snapshotId;
        using (var store = new SqliteAdministrationStore(Database))
        {
            Seed(store); store.EnablePlanning(); var basis = Capture(store); snapshotId = basis.Id;
            var preview = CoursePlanning.PreviewCancellation(basis, ["BIO-LF-BILI"]);
            Assert.Throws<ArgumentException>(() => CoursePlanning.Confirm(store, preview, "demo-beratung", "Demo Streichung", false));
            CoursePlanning.Confirm(store, preview, "demo-beratung", "Demo Streichung", true);
            Assert.Equal(2, store.History("demo-001").Length);
            Assert.Equal(RevisionSource.StudentSubmission, store.History("demo-001")[0].Source);
            Assert.Equal(RevisionSource.OfferCancellation, store.History("demo-001")[1].Source);
            Assert.True(store.Reselection("demo-001").Required);
            Assert.False(store.Reselection("demo-002").Required);
            Assert.All(store.Students(), s => Assert.Equal(preview.Catalog.Version, store.History(s.Student.ExternalStudentId).Last().Snapshot.OfferVersion));
            Assert.Throws<InvalidOperationException>(() => CoursePlanning.Confirm(store, preview, "demo-beratung", "Doppelt", true));
        }
        using var reopened = new SqliteAdministrationStore(Database);
        Assert.True(reopened.PlanningEnabled);
        Assert.Equal(OfferStatus.Cancelled, reopened.ActivePlanningCatalog()!.Offers.Single(o => o.Id == "BIO-LF-BILI").Status);
        Assert.Equal(snapshotId, reopened.PlanningDocuments().Single().Id);
        Assert.Contains(reopened.PlanningDocuments().Single().Choices.Single(c => c.StudentId == "demo-001").Snapshot.Selections, s => s.OfferId == "BIO-LF-BILI");
    }
    [Theory]
    [InlineData("student")]
    [InlineData("choice")]
    [InlineData("followup")]
    public void StalePreviewRejectsEntireBatch(string changed)
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store); store.EnablePlanning();
        var preview = CoursePlanning.PreviewCancellation(Capture(store), ["BIO-LF-BILI"]);
        var record = store.Students().Last();
        if (changed == "student") store.SaveStudent(record with { Version = 2, ChangedAt = DateTimeOffset.UtcNow }, 1);
        if (changed == "choice") new TeacherSession(store, Catalog, "demo-beratung").Restore(record.Student.ExternalStudentId, store.History(record.Student.ExternalStudentId).Last().Id, store.History(record.Student.ExternalStudentId).Last().Id, "Demo Änderung");
        if (changed == "followup") store.SetReselection(record.Student.ExternalStudentId, true, null, "Neue Frist");
        Assert.Throws<InvalidOperationException>(() => CoursePlanning.Confirm(store, preview, "demo-beratung", "Demo", true));
        Assert.Single(store.History("demo-001")); Assert.Null(store.ActivePlanningCatalog()); Assert.Empty(store.PlanningDocuments());
    }
    [Fact]
    public void MidTransactionFailureRollsBackAllRevisionsAndSnapshot()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store); store.EnablePlanning();
        var preview = CoursePlanning.PreviewCancellation(Capture(store), ["BIO-LF-BILI"]);
        var revisions = preview.Changes.Select(c => new ChoiceRevision(Guid.NewGuid(), c.ExpectedActive, c.StudentId, DateTimeOffset.UtcNow,
            "demo", ActorRole.Teacher, RevisionSource.OfferCancellation, "Test", preview.Catalog.Version, "m2-2", c.Choice, [], null)).ToImmutableArray();
        revisions = revisions.SetItem(1, revisions[1] with { PreviousRevisionId = Guid.NewGuid() });
        Assert.Throws<InvalidOperationException>(() => store.CommitCancellation(preview, "demo", "Test", revisions, []));
        Assert.Single(store.History("demo-001")); Assert.Empty(store.PlanningDocuments()); Assert.Null(store.ActivePlanningCatalog());
    }
    [Fact]
    public void SnapshotsAreImmutableAndUnknownSchemaIsRejected()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store); store.EnablePlanning();
        var snapshot = Capture(store); store.SavePlanningDocument(snapshot);
        Assert.ThrowsAny<Exception>(() => store.SavePlanningDocument(snapshot with { Name = "Überschreiben" }));
        Assert.Throws<InvalidDataException>(() => store.SavePlanningDocument(snapshot with { Id = Guid.NewGuid(), SchemaVersion = 99 }));
        Assert.Equal(snapshot.Name, store.PlanningDocuments().Single().Name);
    }
    [Fact]
    public void SavedScenarioKeepsActiveChoicesAndRestorationCreatesNewRevision()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store); store.EnablePlanning();
        var basis = Capture(store) with { TargetGroupSize = 17 };
        var preview = CoursePlanning.PreviewCancellation(basis, ["BIO-LF-BILI"]);
        store.SavePlanningDocument(basis);
        var scenario = CoursePlanning.Scenario(preview, "Demo Szenario"); store.SavePlanningDocument(scenario);
        Assert.True(store.PlanningDocuments().Last().IsScenario);
        Assert.Equal(17, store.PlanningDocuments().Last().TargetGroupSize);
        Assert.NotEqual(basis.Choices[0].Id, scenario.Choices[0].Id);
        Assert.Single(store.History("demo-001")); Assert.Null(store.ActivePlanningCatalog());
        CoursePlanning.Confirm(store, preview, "demo-beratung", "Demo Streichung", true);
        var current = CoursePlanning.Capture(store, preview.Catalog, "demo-beratung", "Nach Streichung", true);
        var restore = CoursePlanning.PreviewOfferRestoration(current, basis);
        CoursePlanning.Confirm(store, restore, "demo-beratung", "Demo Rücknahme Angebot", true);
        Assert.Equal(OfferStatus.Offered, store.ActivePlanningCatalog()!.Offers.Single(o => o.Id == "BIO-LF-BILI").Status);
        Assert.Equal(3, store.History("demo-001").Length);
        Assert.Equal(RevisionSource.Restoration, store.History("demo-001").Last().Source);
        Assert.True(store.Reselection("demo-001").Required);
    }
    [Fact]
    public void CatalogChangeRequiresRedeliveryEvenWithSamePrerequisites()
    {
        using var store = new SqliteAdministrationStore(Database); Seed(store); store.EnablePlanning();
        var record = store.Students().First();
        record = record with { Version = 2, ChangedAt = DateTimeOffset.UtcNow, HomeDirectory = Path.Combine(root, "demo-home"), MappingConfirmed = true };
        Directory.CreateDirectory(record.HomeDirectory); store.SaveStudent(record, 1);
        var distributor = new ProfileDistribution(store); var issued = distributor.Issue(record);
        Assert.Equal("verteilt", distributor.Distribute(record, issued, Catalog).Status);
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, new TeacherSession(store, Catalog, "demo")).Delivered);
        var preview = CoursePlanning.PreviewCancellation(Capture(store), ["BIO-LF-BILI"]);
        CoursePlanning.Confirm(store, preview, "demo", "Demo Streichung", true);
        Assert.Equal(0, TeacherDashboardBuilder.Build(store, new TeacherSession(store, preview.Catalog, "demo")).Delivered);
        Assert.Equal("verteilt", distributor.Distribute(record, issued, preview.Catalog).Status);
        Assert.Equal(1, TeacherDashboardBuilder.Build(store, new TeacherSession(store, preview.Catalog, "demo")).Delivered);
    }
}
