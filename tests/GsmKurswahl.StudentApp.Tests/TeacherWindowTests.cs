using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.ImportExport;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;
using Avalonia.Interactivity;

namespace GsmKurswahl.StudentApp.Tests;

public class TeacherWindowTests
{
    [AvaloniaFact]
    public void ProgressShowsCurrentStepAndDoesNotMarkDeferredDeliveryComplete()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-progress-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(GsmKurswahl.Storage.JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json"))), "demo-beratung");
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Muster", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var window = new TeacherWindow(root);
        void Click(string label)
        {
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
        }
        string? State(int step) => window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == "WorkflowState" + step).Text;
        void Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is not { Length: > 0 } directory) return;
            Directory.CreateDirectory(directory); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap); bitmap.Save(Path.Combine(directory, name + ".png"));
        }
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Equal("Aktueller Schritt", State(0)); Assert.Equal("Noch offen", State(2)); Capture("teacher-offer");
            Click("Fachangebot bestätigen · weiter zur Schülerliste");
            Assert.Equal("Erledigt", State(0)); Assert.Equal("Aktueller Schritt", State(1)); Capture("teacher-import");
            window.JumpToWorkflowStep(2); window.UpdateLayout();
            Assert.Equal("Erledigt", State(1)); Assert.Equal("Aktueller Schritt", State(2)); Capture("teacher-distribution");
            window.JumpToWorkflowStep(0); window.UpdateLayout();
            Assert.Equal("Aktueller Schritt", State(0));
            window.JumpToWorkflowStep(2); window.UpdateLayout();
            Assert.Equal("Aktueller Schritt", State(2));
            Click("Später verteilen · Übersicht öffnen");
            Assert.Equal("Zustellungen offen", State(2)); Capture("teacher-dashboard");
            window.Width = 900; window.Height = 600; window.UpdateLayout(); Capture("teacher-compact");
            var strip = window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "WorkflowProgress");
            var steps = strip.GetLogicalDescendants().OfType<Border>().Where(b => b.Name?.StartsWith("WorkflowStep") == true).ToArray();
            Assert.Equal(3, steps.Length);
            Assert.All(steps, step => Assert.InRange(step.Bounds.Width, 100, strip.Bounds.Width / 3));
            Click("Einstellungen"); Click("Hilfe"); Click("Übersicht");
            Assert.Equal("Zustellungen offen", State(2));
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public async Task AutomaticCollectionImportsOnceAndLeavesTeacherConflictsOpen()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-auto-" + Guid.NewGuid());
        var home = Path.Combine(root, "homes", "10a", "Muster.Demo"); Directory.CreateDirectory(home);
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.Catalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Muster", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "S-1-5-21-100-200-300-1", home, true, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var path = Path.Combine(home, "aktuell.json");
        File.WriteAllBytes(path, GsmKurswahl.Storage.JsonDocuments.WriteChoice(TestData.Choice));
        var window = new TeacherWindow(root);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        async Task Collect()
        {
            await (Task)typeof(TeacherWindow).GetMethod("CollectAutomatically", flags)!.Invoke(window, null)!;
            for (var i = 0; i < 200 && (bool)typeof(TeacherWindow).GetField("collecting", flags)!.GetValue(window)!; i++) await Task.Delay(10);
        }
        try
        {
            window.Show(); await Collect();
            var store = (IAdministrationStore)typeof(TeacherWindow).GetField("store", flags)!.GetValue(window)!;
            Assert.Single(store.History("demo-001"));
            await Collect(); Assert.Single(store.History("demo-001"));
            var session = new GsmKurswahl.Application.TeacherSession(store, TestData.Catalog, "demo-beratung");
            session.Save(TestData.Choice, store.History("demo-001").Last().Id, RevisionSource.TeacherEdit, "Synthetische Korrektur");
            File.WriteAllBytes(path, GsmKurswahl.Storage.JsonDocuments.WriteChoice(TestData.Choice with { Revision = TestData.Choice.Revision + 1 }));
            await Collect(); Assert.Equal(2, store.History("demo-001").Length);
            Assert.Equal(RevisionSource.TeacherEdit, store.History("demo-001").Last().Source);
            var pending = (System.Collections.Immutable.ImmutableArray<GsmKurswahl.Application.CollectionPreview>)typeof(TeacherWindow).GetField("collections", flags)!.GetValue(window)!;
            Assert.Single(pending); Assert.NotEmpty(pending[0].Conflict);
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void SettingsHelpAndReturnCanBeVisitedRepeatedlyBeforeSetup()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-nav-" + Guid.NewGuid());
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            for (var i = 0; i < 3; i++)
                foreach (var label in new[] { "Einstellungen", "Hilfe", "Einstellungen", "Zur Einrichtung" })
                {
                    window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.UpdateLayout();
                }
            Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), b => b.Name == "CreateYear");
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Vorgang nicht abgeschlossen") == true);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void ImportPreselectsAllValidRowsAndSavesThemInOneAction()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-batch-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Import });
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            var table = StudentTableImport.Csv("studentId;firstName;lastName;class;year;username;imp\ndemo-001;Demo;Eins;10a;2029;DEMO\\test1;ja\ndemo-002;Demo;Zwei;10b;2029;DEMO\\test2;");
            var origin = new ImportOrigin("demo.csv", "synthetic-batch", "v1", DateTimeOffset.UtcNow, "demo-beratung");
            var rows = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), origin, "demo-beratung");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(TeacherWindow).GetField("importRows", flags)!.SetValue(window, rows);
            typeof(TeacherWindow).GetMethod("ShowImportPreview", flags)!.Invoke(window, null);
            window.UpdateLayout();
            Assert.Equal(2, window.GetLogicalDescendants().OfType<CheckBox>().Count(c => c.IsChecked == true));
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "AcceptStudentBatch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var store = (IAdministrationStore)typeof(TeacherWindow).GetField("store", flags)!.GetValue(window)!;
            Assert.Equal(2, store.Students().Length);
            Assert.Equal("10a/Eins.Demo", store.Students().Single(s => s.Student.ExternalStudentId == "demo-001").HomeDirectory);
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void TeacherWindowStartsAndExposesAdministrativeWorkflow()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-teacher-ui-" + Guid.NewGuid());
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Contains("M3", window.Title);
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Abiturjahrgang einrichten");
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "CreateYear").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            Assert.Equal(YearSetupStep.Offer, new TeacherWorkspace(root).Active!.Step);
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "ConfirmSetupOffer").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(YearSetupStep.Import, new TeacherWorkspace(root).Active!.Step);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void CompletedWorkspaceAutomaticallyShowsNextTaskAndPersistsOnRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-teacher-ui-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        using (var store = workspace.OpenActive()) store.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10-demo", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Import });
        var setup = new TeacherWindow(root);
        try
        {
            setup.Show(); setup.UpdateLayout();
            void Click(string label) => setup.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            setup.JumpToWorkflowStep(2); setup.UpdateLayout();
            Assert.Equal(YearSetupStep.Mapping, new TeacherWorkspace(root).Active!.Step);
            Click("Später verteilen · Übersicht öffnen");
            Assert.Equal(YearSetupStep.Complete, new TeacherWorkspace(root).Active!.Step);
        }
        finally { setup.Close(); }
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Ihr aktueller Jahrgang");
            Assert.Equal("Schülerdateien verteilen", window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "NextTask").Content);
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Button>(), b => b.Name == "CreateYear");
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void ColumnPickerMapsSelectedHeadersAndClearsPreviousFile()
    {
        var picker = new ImportColumnPicker();
        var window = new Window { Content = picker };
        try
        {
            window.Show();
            picker.Load(new ImportTable(["studentId", "Anzeigename", "IMP"], []));
            var boxes = picker.GetLogicalDescendants().OfType<ComboBox>().ToDictionary(b => b.Name!);
            Assert.Equal("studentId", picker.Read()["studentId"]);
            Assert.DoesNotContain("imp", picker.Read().Keys);
            boxes["Column_name"].SelectedIndex = 2;
            boxes["Column_imp"].SelectedIndex = 3;
            Assert.Equal("Anzeigename", picker.Read()["name"]);
            Assert.Equal("IMP", picker.Read()["imp"]);
            Assert.NotNull(ToolTip.GetTip(boxes["Column_imp"]));
            picker.Load(new ImportTable(["Andere Spalte"], []));
            Assert.Empty(picker.Read());
            picker.Clear(); Assert.Empty(picker.Read());
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void StudentDetailsAllowPrerequisiteEditingAndPreserveRelativeHomeDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-edit-student-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            Click("Schülerbestand");
            Click("Öffnen");
            var userBox = window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "StudentUsername");
            userBox.Text = "DEMO\\test2";
            Click("Zuordnung speichern");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var store = (IAdministrationStore)typeof(TeacherWindow).GetField("store", flags)!.GetValue(window)!;
            var afterMapping = store.Students().Single();
            Assert.Equal("10a/Eins.Demo", afterMapping.HomeDirectory);
            Assert.Equal("DEMO\\test2", afterMapping.WindowsUsername);
            var impBox = window.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "PrereqImp");
            impBox.SelectedItem = Knowledge.Yes;
            var departureBox = window.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "StudentDeparture");
            departureBox.SelectedItem = DepartureStatus.ExpectedDeparture;
            var sportBox = window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "PrereqSportSemesters");
            sportBox.Text = "2,1";
            var langBox = window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "PrereqLanguages");
            langBox.Text = "E, F";
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "SavePrerequisites").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            var updated = store.Students().Single();
            Assert.Equal(3, updated.Version);
            Assert.Equal(Knowledge.Yes, updated.Student.Prerequisites.InformaticsImp);
            Assert.Equal(DepartureStatus.ExpectedDeparture, updated.Student.Departure);
            Assert.Equal([1, 2], updated.Student.Prerequisites.SportExemptSemesters!.Value.ToArray());
            Assert.Equal(["E", "F"], updated.Student.Prerequisites.ContinuedLanguages.ToArray());
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public void SimpleDistributionDeliversProfilesWithoutCertificateOrSid()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-distribute-ui-" + Guid.NewGuid());
        var targetFolder = Path.Combine(root, "homes", "10a", "Eins.Demo");
        Directory.CreateDirectory(targetFolder);
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Mapping });
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes }), "", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "DistributionRoot").Text = Path.Combine(root, "homes");
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            Click("Ziele prüfen und gemeinsame Verteilung vorbereiten");
            Click("1 Zuordnungen bestätigen und Schülerdateien verteilen");
            Assert.True(File.Exists(Path.Combine(targetFolder, "profil.json")));
            Assert.False(File.Exists(Path.Combine(targetFolder, "profil.sig")));
            Assert.Equal(YearSetupStep.Complete, new TeacherWorkspace(root).Active!.Step);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
