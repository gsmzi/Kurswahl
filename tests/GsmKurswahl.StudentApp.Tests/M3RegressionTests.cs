using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using GsmKurswahl.Application;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.Tests;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class M3RegressionTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "gsm-review-" + Guid.NewGuid());
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public M3RegressionTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    StudentRecord Record(string id = "demo-001", bool confirmed = false) => new(1,
        new(Guid.NewGuid(), id, "Demo Muster", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
        "", "", Path.Combine(root, "10a", "Muster.Demo"), confirmed, 1, "demo-review", DateTimeOffset.UtcNow, null);
    TeacherWorkspace Workspace(StudentRecord record, YearSetupStep step = YearSetupStep.Complete)
    {
        var workspace = new TeacherWorkspace(Path.Combine(root, "workspace")); workspace.Create(TestData.WorkflowCatalog, "demo-review");
        workspace.Update(workspace.Active! with { Step = step });
        using var store = workspace.OpenActive(); store.SaveStudent(record, null); return workspace;
    }
    static T Field<T>(TeacherWindow w, string name) => (T)typeof(TeacherWindow).GetField(name, Flags)!.GetValue(w)!;
    static void Click(TeacherWindow w, string label) => w.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static T Control<T>(TeacherWindow w, string name) where T : Avalonia.Controls.Control => w.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
    static async Task Collect(TeacherWindow w)
    {
        for (int i=0; i<200 && Field<bool>(w,"collecting");i++) await Task.Delay(10);
        await (Task)typeof(TeacherWindow).GetMethod("CollectAutomatically", Flags)!.Invoke(w, null)!;
    }
    [AvaloniaFact]
    public async Task BackgroundCollectionMustPreserveUnsavedPrerequisiteEdit()
    {
        var workspace = Workspace(Record()); var window = new TeacherWindow(workspace.Root);
        try
        {
            window.Show(); Click(window, "Schülerbestand"); Click(window, "Öffnen");
            Control<ComboBox>(window, "PrereqImp").SelectedItem = Knowledge.Yes;
            await Collect(window);
            Assert.Equal(Knowledge.Yes, Control<ComboBox>(window, "PrereqImp").SelectedItem);
        }
        finally { window.Close(); }
    }
    [Fact]
    public void ChangingConfirmedFolderMustInvalidatePreviousDelivery()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root, "db.db"));
        var record = Record(confirmed:true); Directory.CreateDirectory(record.HomeDirectory); store.SaveStudent(record, null);
        var service = new ProfileDistribution(store); var profile = service.Issue(record);
        Assert.Equal("verteilt", service.Distribute(record,profile).Status);
        var moved = record with { Version = 2, ChangedAt = DateTimeOffset.UtcNow.AddSeconds(1), HomeDirectory = Path.Combine(root,"10b","Muster.Demo") };
        Directory.CreateDirectory(moved.HomeDirectory); store.SaveStudent(moved,1);
        Assert.False(File.Exists(Path.Combine(moved.HomeDirectory,"profil.json")));
        var summary = TeacherDashboardBuilder.Build(store,new TeacherSession(store,TestData.WorkflowCatalog,"demo-review"));
        Assert.Equal(0,summary.Delivered);
    }
    [AvaloniaFact]
    public async Task BackgroundCollectionMustPreserveExcludedConflict()
    {
        var record = Record(confirmed:true); Directory.CreateDirectory(record.HomeDirectory); var workspace = Workspace(record);
        using(var store = workspace.OpenActive())
            new TeacherSession(store, TestData.WorkflowCatalog,"demo-review").Save(TestData.Choice,null,RevisionSource.TeacherEdit,"Demo Lehrerentscheidung");
        File.WriteAllBytes(Path.Combine(record.HomeDirectory,"aktuell.json"),JsonDocuments.WriteChoice(TestData.Choice));
        var window = new TeacherWindow(workspace.Root);
        try
        {
            window.Show(); await Collect(window);
            typeof(TeacherWindow).GetMethod("ShowPage",Flags)!.Invoke(window,["collection"]);
            var results = Field<StackPanel>(window,"collectionResults");
            results.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked = false;
            await Collect(window);
            Assert.False(results.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void OfferEditorMustRejectChangesAfterFirstCollectionArrives()
    {
        var workspace = Workspace(Record(), YearSetupStep.Offer); var window = new TeacherWindow(workspace.Root);
        try
        {
            window.Show();
            var store = Field<IAdministrationStore>(window,"store"); var session = Field<TeacherSession>(window,"session");
            session.Save(TestData.Choice,null,RevisionSource.StudentSubmission,"Demo automatisch eingesammelt");
            var before = new TeacherWorkspace(workspace.Root).Active!.Catalog.Version;
            Control<CheckBox>(window,"SetupOffer_W_LF").IsChecked = false;
            Assert.Equal(before,new TeacherWorkspace(workspace.Root).Active!.Catalog.Version);
        }
        finally { window.Close(); }
    }
    [Fact]
    public void DistributionIntoStudentFolderMustBeFoundByStudentSettings()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root,"db.db"));
        var record = Record(confirmed:true); Directory.CreateDirectory(record.HomeDirectory); store.SaveStudent(record,null);
        var service = new ProfileDistribution(store); Assert.Equal("verteilt",service.Distribute(record,service.Issue(record),TestData.WorkflowCatalog).Status);
        var settings = new StudentSettings(2,record.HomeDirectory,"","aktuell.json","{WAHLORDNER}/fachangebot.json","");
        Assert.Equal(TestData.WorkflowCatalog.Version, JsonDocuments.ReadCatalog(File.ReadAllBytes(settings.ResolveCatalogPath(Path.Combine(root, "appsettings.json")))).Version);
        Assert.Equal(ProfileStatus.Verified,settings.ReadPersonalProfile(2029).Profile.Status);
    }
    [Fact]
    public void SharedStudentConfigurationMustOpenSecondDistributedStudent()
    {
        using var store = new SqliteAdministrationStore(Path.Combine(root,"db.db"));
        var record = Record("demo-002",true); Directory.CreateDirectory(record.HomeDirectory); store.SaveStudent(record,null);
        var service = new ProfileDistribution(store); Assert.Equal("verteilt",service.Distribute(record,service.Issue(record),TestData.WorkflowCatalog).Status);
        var settings = new StudentSettings(2, record.HomeDirectory, "", "aktuell.json", "{WAHLORDNER}/fachangebot.json", ""); var personal = settings.ReadPersonalProfile(2029); Assert.Equal("demo-002", personal.StudentId); Assert.Equal(ProfileStatus.Verified, personal.Profile.Status); Assert.Equal(ProfileStatus.ProfileInvalid, new SignedProfileReader().Read(record.HomeDirectory, "demo-001", 2029).Status);
    }
}
