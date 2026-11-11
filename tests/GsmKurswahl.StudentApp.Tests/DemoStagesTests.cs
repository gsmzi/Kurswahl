using System.Reflection;
using Avalonia.Headless.XUnit;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;
using GsmKurswahl.TeacherApp;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class DemoStagesTests
{
    private static OfferCatalog Catalog => JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
    private static void InStage(string id, Action<string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-stage-" + Guid.NewGuid());
        try { DemoStages.Prepare(id, root, Catalog); check(root); }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static int ChoiceFiles(string root) => Directory.GetFiles(Path.Combine(root, DemoStages.DistributionFolder), "aktuell.json", SearchOption.AllDirectories).Length;
    private static int ProfileFiles(string root) => Directory.GetFiles(Path.Combine(root, DemoStages.DistributionFolder), "profil.json", SearchOption.AllDirectories).Length;
    // Opening the window starts a collection of its own; a second call would return at once while it runs.
    private static async Task Collect(TeacherWindow window)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var collecting = typeof(TeacherWindow).GetField("collecting", flags)!;
        for (var i = 0; i < 12000 && (bool)collecting.GetValue(window)!; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.False((bool)collecting.GetValue(window)!);
        await (Task)typeof(TeacherWindow).GetMethod("CollectAutomatically", flags)!.Invoke(window, null)!;
    }

    [Fact]
    public void StartStageOffersImportFileAndClassFoldersButNoYear() => InStage("lehrer-1-anfang", root =>
    {
        Assert.Empty(new TeacherWorkspace(root).Document.Years);
        Assert.Equal(81, File.ReadAllLines(Path.Combine(root, DemoStages.ImportFile)).Length);
        Assert.Equal(80, Directory.GetDirectories(Path.Combine(root, DemoStages.DistributionFolder), "Testperson*", SearchOption.AllDirectories).Length);
        var presets = SchoolDefaults.Load(root);
        Assert.Equal(Path.Combine(root, DemoStages.DistributionFolder), presets.DistributionRoot);
        Assert.Equal("", presets.StudentProgramDirectory);
    });

    [Fact]
    public void ImportedStageHasStudentsWithoutDelivery() => InStage("lehrer-2-importiert", root =>
    {
        var workspace = new TeacherWorkspace(root);
        Assert.Equal(YearSetupStep.Mapping, workspace.Active!.Step);
        using var store = workspace.OpenActive();
        Assert.Equal(80, store.Students().Length);
        Assert.All(store.Students(), s => { Assert.False(s.MappingConfirmed); Assert.NotNull(s.Origin); });
        Assert.Equal(0, ProfileFiles(root));
    });

    [Fact]
    public void DistributedStageHasProfilesAndNoChoices() => InStage("lehrer-3-ausgeteilt", root =>
    {
        var workspace = new TeacherWorkspace(root);
        Assert.Equal(YearSetupStep.Complete, workspace.Active!.Step);
        using var store = workspace.OpenActive();
        Assert.All(store.Students(), s => Assert.True(s.MappingConfirmed));
        Assert.Equal(80, ProfileFiles(root)); Assert.Equal(0, ChoiceFiles(root));
    });

    [AvaloniaFact]
    public async Task SubmittedChoicesArriveWhenTheTeacherProgramOpens()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-stage-" + Guid.NewGuid());
        try
        {
            DemoStages.Prepare("lehrer-4-wahlen-abgegeben", root, Catalog);
            Assert.Equal(DemoStages.SubmittedBeforeCollection, ChoiceFiles(root));
            var window = new TeacherWindow(root);
            try { window.Show(); await Collect(window); }
            finally { window.Close(); }
            var workspace = new TeacherWorkspace(root);
            using var store = workspace.OpenActive();
            var states = new TeacherSession(store, workspace.Active!.Catalog, "demo-test").Overview();
            Assert.Equal(DemoStages.SubmittedBeforeCollection, states.Count(s => s.Active is not null));
            Assert.DoesNotContain(states, s => s.Validation?.Messages.Any(m => m.Rule.Severity == ReviewStatus.Invalid) == true);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task CollectedStageIsNotCollectedASecondTime()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-stage-" + Guid.NewGuid());
        try
        {
            DemoStages.Prepare("lehrer-5-eingesammelt", root, Catalog);
            var window = new TeacherWindow(root);
            try { window.Show(); await Collect(window); }
            finally { window.Close(); }
            var workspace = new TeacherWorkspace(root);
            using var store = workspace.OpenActive();
            var states = new TeacherSession(store, workspace.Active!.Catalog, "demo-test").Overview();
            Assert.Equal(80, states.Length);
            Assert.All(states, s => Assert.Single(store.History(s.Record.Student.ExternalStudentId)));
            Assert.Equal(3, store.PlanningDocuments().Length);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public async Task ReselectionStageShowsCancelledOfferAndOpensWithoutLosingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-stage-" + Guid.NewGuid());
        try
        {
            DemoStages.Prepare("lehrer-6-nachwahl", root, Catalog);
            var window = new TeacherWindow(root);
            try { window.Show(); await Collect(window); }
            finally { window.Close(); }
            var workspace = new TeacherWorkspace(root);
            Assert.NotEqual(OfferStatus.Offered, workspace.Active!.Catalog.Offers.Single(o => o.Id == "BIO-LF-BILI").Status);
            using var store = workspace.OpenActive();
            var states = new TeacherSession(store, workspace.Active.Catalog, "demo-test").Overview();
            Assert.Contains(states, s => s.RequiresReselection);
            Assert.DoesNotContain(states, s => s.Active!.Snapshot.Selections.Any(c => c.OfferId == "BIO-LF-BILI"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("schueler-1-blanko", false, 0)]
    [InlineData("schueler-2-angefangen", true, 3)]
    [InlineData("schueler-3-fertig", true, 10)]
    [InlineData("schueler-4-alle-voraussetzungen", false, 0)]
    [InlineData("schueler-5-sportbefreit", false, 0)]
    [InlineData("schueler-6-ohne-zusatzrechte", false, 0)]
    [InlineData("schueler-7-vorbildung-unbekannt", false, 0)]
    public void PupilStagesLoadThroughTheProductiveReaders(string id, bool hasChoice, int minimumSelections) => InStage(id, root =>
    {
        var data = StudentFolderLayout.Data(root);
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(data, "fachangebot.json")));
        var (studentId, profile) = new SignedProfileReader().ReadPersonal(data, catalog.GraduationYear);
        Assert.Equal("demo-002", studentId); Assert.Equal(ProfileStatus.Verified, profile.Status);
        var loaded = new JsonChoiceStore(Path.Combine(data, "aktuell.json"), data).LoadAsync().GetAwaiter().GetResult();
        Assert.Equal(hasChoice, loaded is not null);
        if (loaded is not null) Assert.True(loaded.Choice.Selections.Length >= minimumSelections);
        if (id == "schueler-4-alle-voraussetzungen") Assert.Equal(Knowledge.Yes, profile.Prerequisites!.ContinuousBilingual);
        if (id == "schueler-5-sportbefreit") Assert.Equal([3, 4], profile.Prerequisites!.SportExemptSemesters!.Value.ToArray());
        if (id == "schueler-6-ohne-zusatzrechte")
        {
            Assert.Equal(Knowledge.No, profile.Prerequisites.ItalianProfile);
            Assert.Equal(Knowledge.No, profile.Prerequisites.InformaticsBridge);
            Assert.Equal(Knowledge.No, profile.Prerequisites.InformaticsImp);
            Assert.Equal(Knowledge.No, profile.Prerequisites.ContinuousBilingual);
        }
        if (id == "schueler-7-vorbildung-unbekannt")
        {
            Assert.Equal(Knowledge.Unknown, profile.Prerequisites.ItalianProfile);
            Assert.Equal(Knowledge.Unknown, profile.Prerequisites.InformaticsBridge);
            Assert.Equal(Knowledge.Unknown, profile.Prerequisites.InformaticsImp);
            Assert.Equal(Knowledge.Unknown, profile.Prerequisites.ContinuousBilingual);
        }
    });

    [Fact]
    public void ReopeningKeepsTesterChanges() => InStage("schueler-1-blanko", root =>
    {
        var note = Path.Combine(StudentFolderLayout.Data(root), "aktuell.json");
        File.WriteAllText(note, "Änderung einer Testperson");
        DemoStages.Prepare("schueler-1-blanko", root, Catalog);
        Assert.Equal("Änderung einer Testperson", File.ReadAllText(note));
    });

    [Fact]
    public void ForeignFolderAndUnknownStageAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-stage-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "existing.txt"), "keep");
            Assert.Throws<IOException>(() => DemoStages.Prepare("lehrer-1-anfang", root, Catalog));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "existing.txt")));
            Assert.Throws<ArgumentException>(() => DemoStages.Prepare("gibt-es-nicht", root, Catalog));
        }
        finally { Directory.Delete(root, true); }
    }
}
