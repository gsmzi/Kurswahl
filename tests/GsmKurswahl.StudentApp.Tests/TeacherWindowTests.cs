using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.ImportExport;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;
using Avalonia.Interactivity;

namespace GsmKurswahl.StudentApp.Tests;

public class TeacherWindowTests
{
    [AvaloniaTheory]
    [InlineData("ER", "KR", false)]
    [InlineData("KR", "ER", false)]
    [InlineData("ETH", "KR", false)]
    [InlineData(null, "KR", false)]
    [InlineData("ER", "KR", true)]
    public void TeacherEditorRestrictsReligionAndPreservesInvalidEntriesForRemoval(string? grade10, string other, bool stored)
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-religion-editor-" + Guid.NewGuid());
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects, new("ER", "Evangelische Religion"), new("KR", "Katholische Religion")],
            Offers = [.. TestData.WorkflowCatalog.Offers, TestData.Offer("ER-BF", "ER"), TestData.Offer("KR-BF", "KR")]
        };
        var workspace = new TeacherWorkspace(root); workspace.Create(catalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
        {
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active,
                PriorEducation.Unknown with { ReligionEthicsGrade10 = grade10, ReligionEthicsPermission = Knowledge.Yes }), "", "", "10a/Eins.Demo", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            if (stored) new GsmKurswahl.Application.TeacherSession(db, catalog, "demo-beratung").Save(TestData.Choice with
            {
                Selections = [new(other + "-BF", other, CourseType.Bf, CourseVariant.Normal, [1])], PlannedOralSubjects = [other]
            }, null, RevisionSource.TeacherEdit, "Synthetischer Bestandsfall");
        }
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            Click("Wahl"); Click("Schülerbestand"); Click("Öffnen");
            var boxes = window.GetLogicalDescendants().OfType<CheckBox>().ToArray();
            Assert.Contains(boxes, b => b.Name == "TeacherChoice_ETH_BF_1" && b.IsEnabled);
            if (grade10 is "ER" or "KR")
                Assert.Contains(boxes, b => b.Name == $"TeacherChoice_{grade10}_BF_1" && b.IsEnabled);
            var wrong = boxes.Where(b => b.Name?.StartsWith($"TeacherChoice_{other}_BF_") == true).ToArray();
            if (stored)
            {
                Assert.Equal(4, wrong.Length);
                var first = wrong.Single(b => b.Name!.EndsWith("_1"));
                Assert.True(first.IsChecked); Assert.True(first.IsEnabled);
                Assert.All(wrong.Where(b => b != first), b => Assert.False(b.IsEnabled));
                first.IsChecked = false;
                Assert.False(first.IsEnabled);
            }
            else if (grade10 is null)
            {
                Assert.Equal(4, wrong.Length);
                Assert.All(wrong, b => Assert.True(b.IsEnabled));
                Assert.Contains(boxes, b => b.Name == "TeacherChoice_ER_BF_1" && b.IsEnabled);
            }
            else Assert.Empty(wrong);
            foreach (var box in window.GetLogicalDescendants().OfType<ComboBox>().Where(b => b.Name is "OralSubject1" or "OralSubject2"))
            {
                var items = box.Items.OfType<ComboBoxItem>().ToArray();
                if (grade10 is null)
                    Assert.Contains(items, i => i.Content?.ToString() == "Katholische Religion" && i.IsEnabled);
                else Assert.DoesNotContain(items, i => i.Content?.ToString() == (other == "ER" ? "Evangelische Religion" : "Katholische Religion") && i.IsEnabled);
                if (stored && box.Name == "OralSubject1") Assert.False(((ComboBoxItem)box.SelectedItem!).IsEnabled);
            }
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public void ProgressShowsCurrentStepAndDoesNotMarkDeferredDeliveryComplete()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-progress-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(GsmKurswahl.Storage.JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json"))), "demo-beratung");
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Muster", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
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
        void AssertLogoFits()
        {
            var logo = window.GetLogicalDescendants().OfType<Image>().Single(i => i.Name == "SchoolLogo");
            Assert.True(logo.IsVisible);
            Assert.True(logo.Bounds.Width > 100 && logo.Bounds.Height > 35);
            var origin = logo.TranslatePoint(default, window);
            Assert.NotNull(origin);
            Assert.True(origin.Value.X >= 0 && origin.Value.X + logo.Bounds.Width <= window.ClientSize.Width + 1);
        }
        try
        {
            window.Show(); window.UpdateLayout();
            AssertLogoFits();
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
            Assert.False(window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "WorkflowProgress").IsVisible); Capture("teacher-dashboard"); Click("Wahl"); Assert.Equal("Zustellungen offen", State(2));
            window.Width = 900; window.Height = 600; window.UpdateLayout(); Capture("teacher-compact");
            AssertLogoFits();
            var strip = window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "WorkflowProgress");
            var steps = strip.GetLogicalDescendants().OfType<Border>().Where(b => b.Name?.StartsWith("WorkflowStep") == true).ToArray();
            Assert.Equal(4, steps.Length);
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
        var home = Path.Combine(root, "homes", "10a", "Muster.Demo"); Directory.CreateDirectory(StudentFolderLayout.Data(home));
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.Catalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Muster", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "S-1-5-21-100-200-300-1", home, true, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var path = Path.Combine(StudentFolderLayout.Data(home), "aktuell.json");
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
            Assert.Contains("Lehrerverwaltung", window.Title);
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
        using (var store = workspace.OpenActive()) store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10-demo", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
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
            picker.Load(new ImportTable(["studentId", "Personenbezeichnung", "Zusatzfeld", "IMP"], []));
            var boxes = picker.GetLogicalDescendants().OfType<ComboBox>().ToDictionary(b => b.Name!);
            Assert.Equal("studentId", picker.Read()["studentId"]);
            Assert.Equal("IMP", picker.Read()["imp"]);
            Assert.DoesNotContain("name", picker.Read().Keys);
            Assert.DoesNotContain("bridge", picker.Read().Keys);
            boxes["Column_name"].SelectedIndex = 2;
            boxes["Column_bridge"].SelectedIndex = 3;
            Assert.Equal("Personenbezeichnung", picker.Read()["name"]);
            Assert.Equal("Zusatzfeld", picker.Read()["bridge"]);
            Assert.NotNull(ToolTip.GetTip(boxes["Column_imp"]));
            picker.Load(new ImportTable(["ASV-ID", "Nachname", "Vornamen"], []));
            Assert.Equal("ASV-ID", picker.Read()["studentId"]);
            Assert.Equal("Nachname", picker.Read()["lastName"]);
            Assert.Equal("Vornamen", picker.Read()["firstName"]);
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
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "DEMO\\test1", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            Click("Wahl"); Click("Schülerbestand");
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
    public async Task SimpleDistributionDeliversProfilesWithoutCertificateOrSid()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-distribute-ui-" + Guid.NewGuid());
        var targetFolder = Path.Combine(root, "homes", "10a", "Eins.Demo");
        Directory.CreateDirectory(targetFolder);
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Mapping });
        using (var db = workspace.OpenActive())
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes }), "", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
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
            // Distribution runs in the background; the window stays responsive and shows progress until it ends.
            var progress = window.GetLogicalDescendants().OfType<ProgressBar>().Single(p => p.Name == "BusyProgress");
            for (var wait = 0; wait < 500 && new TeacherWorkspace(root).Active!.Step != YearSetupStep.Complete; wait++)
            { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(progress.Maximum, progress.Value);
            Assert.False(((Control)progress.Parent!).IsVisible);
            Assert.True(File.Exists(Path.Combine(StudentFolderLayout.Data(targetFolder), "profil.json")));
            Assert.False(File.Exists(Path.Combine(StudentFolderLayout.Data(targetFolder), "profil.sig")));
            Assert.Equal(YearSetupStep.Complete, new TeacherWorkspace(root).Active!.Step);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public void ReimportPreviewDoesNotPreselectManuallyCorrectedStudent()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-reimport-ui-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Import });
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            void PreviewCsv(string csv, string hash)
            {
                var table = StudentTableImport.Csv(csv);
                var origin = new ImportOrigin("demo.csv", hash, "v1", DateTimeOffset.UtcNow, "demo-beratung");
                var rows = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), origin, "demo-beratung");
                typeof(TeacherWindow).GetField("importRows", flags)!.SetValue(window, rows);
                typeof(TeacherWindow).GetMethod("ShowImportPreview", flags)!.Invoke(window, null);
                window.UpdateLayout();
            }

            PreviewCsv("studentId;firstName;lastName;class;year;username;imp\ndemo-001;Demo;Eins;10a;2029;DEMO\\test1;\ndemo-002;Demo;Zwei;10a;2029;DEMO\\test2;", "hash1");
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "AcceptStudentBatch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();

            var store = (IAdministrationStore)typeof(TeacherWindow).GetField("store", flags)!.GetValue(window)!;
            var d1 = store.Students().Single(s => s.Student.ExternalStudentId == "demo-001");
            store.SaveStudent(d1 with { Version = 2, Student = d1.Student with { Prerequisites = d1.Student.Prerequisites with { InformaticsImp = Knowledge.Yes } } }, 1);

            PreviewCsv("studentId;firstName;lastName;class;year;username;imp\ndemo-001;Demo;Eins;10a;2029;DEMO\\test1;\ndemo-002;Demo;Zwei;10b;2029;DEMO\\test2;\ndemo-003;Demo;Drei;10c;2029;DEMO\\test3;", "hash2");
            var boxes = window.GetLogicalDescendants().OfType<CheckBox>().ToArray();
            var box1 = boxes.Single(c => c.Name == "ImportSelect_demo-001");
            var box2 = boxes.Single(c => c.Name == "ImportSelect_demo-002");
            var box3 = boxes.Single(c => c.Name == "ImportSelect_demo-003");
            Assert.False(box1.IsChecked);
            Assert.True(box1.IsEnabled);
            Assert.True(box2.IsChecked);
            Assert.True(box3.IsChecked);

            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "AcceptStudentBatch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();

            var students = store.Students();
            var kept = students.Single(s => s.Student.ExternalStudentId == "demo-001");
            Assert.Equal(Knowledge.Yes, kept.Student.Prerequisites.InformaticsImp);
            Assert.Equal(2, kept.Version);
            Assert.Equal("10b", students.Single(s => s.Student.ExternalStudentId == "demo-002").Student.ClassName);
            Assert.Equal("10c", students.Single(s => s.Student.ExternalStudentId == "demo-003").Student.ClassName);
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public void StudentDetailsPreserveUnknownSelectionsAndRejectInvalidSportSemesters()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-preserve-details-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
        {
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
                "", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            new GsmKurswahl.Application.TeacherSession(db, TestData.WorkflowCatalog, "demo-beratung").Save(TestData.Choice with
            {
                Selections =
                [
                    new("ALT-WF", "ALT", CourseType.Elective, CourseVariant.Normal, [1, 2]),
                    new("GEO-BF", "GEO", CourseType.Bf, CourseVariant.Normal, [1, 3, 4])
                ],
                PlannedOralSubjects = ["W", "D"]
            }, null, RevisionSource.TeacherEdit, "Synthetischer Altbestand");
        }
        var window = new TeacherWindow(root);
        try
        {
            window.Show(); window.UpdateLayout();
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            Click("Wahl"); Click("Schülerbestand"); Click("Öffnen");

            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var store = (IAdministrationStore)typeof(TeacherWindow).GetField("store", flags)!.GetValue(window)!;
            var sportBox = window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "PrereqSportSemesters");
            sportBox.Text = "1, 5";
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "SavePrerequisites").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            Assert.Equal(1, store.Students().Single().Version);
            Assert.Null(store.Students().Single().Student.Prerequisites.SportExemptSemesters);

            window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Name == "StudentEditReason").Text = "Reguläres Fach ergänzen";
            window.GetLogicalDescendants().OfType<CheckBox>().Single(b => b.Name == "TeacherChoice_D_BF_1").IsChecked = true;
            window.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "SaveTeacherChoice").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();

            var saved = store.History("demo-001").Last().Snapshot;
            Assert.Equal([1, 2], saved.Selections.Single(s => s.OfferId == "ALT-WF").Semesters.ToArray());
            Assert.Equal([1, 3, 4], saved.Selections.Single(s => s.OfferId == "GEO-BF").Semesters.ToArray());
            Assert.Equal([1], saved.Selections.Single(s => s.OfferId == "D-BF").Semesters.ToArray());
            Assert.Equal(["W", "D"], saved.PlannedOralSubjects.ToArray());
        }
        finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [AvaloniaFact]
    public void UsabilityAlertsExpandersFiltersAndStudentDetailWork()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-usability-ui-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root);
        workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var db = workspace.OpenActive())
        {
            db.EnablePlanning();
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
                "", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            db.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-002", "Demo Zwei", "10b", 2029, DepartureStatus.Active, PriorEducation.Unknown),
                "", "", "10b/Zwei.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            new GsmKurswahl.Application.TeacherSession(db, TestData.WorkflowCatalog, "demo-beratung").Save(TestData.Choice with
            {
                Selections =
                [
                    TestData.Select(TestData.WorkflowCatalog.Offers.Single(o => o.Id == "D-LF")),
                    TestData.Select(TestData.WorkflowCatalog.Offers.Single(o => o.Id == "BIO-LF"))
                ]
            }, null, RevisionSource.TeacherEdit, "Synthetischer Ausgangsstand");
        }

        var window = new TeacherWindow(root) { Width = 1400, Height = 900 };
        try
        {
            window.Show();
            window.UpdateLayout();
            IEnumerable<T> All<T>() where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
            T Named<T>(string name) where T : Control => All<T>().Single(c => c.Name == name);
            void Click(string label)
            {
                All<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            void ClickNamed(string name)
            {
                Named<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }

            var alertBanner = Named<Border>("AlertBanner");

            // Student details: inline errors + banner + stale error clearing + RestoreReason + no broken tab reference
            Click("Wahl");
            Click("Schülerbestand");
            All<Button>().First(b => b.Content?.ToString() == "Öffnen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            Assert.Contains(All<TextBox>(), t => t.Name == "RestoreReason");
            Assert.DoesNotContain(All<TextBlock>(), t => t.Text?.Contains("im Reiter „Kurswahl und Nachwahl“") == true);

            Named<TextBox>("PrereqSportSemesters").Text = "1, 5";
            ClickNamed("SavePrerequisites");
            Assert.True(Named<Border>("StudentPrereqError").IsVisible);
            Assert.True(alertBanner.IsVisible);

            // Navigating to another page clears stale error banners
            Click("Kurswahlen");
            Assert.False(alertBanner.IsVisible);

            // Choices: filter reset & inline reason validation on save
            Assert.Equal(2, Named<StackPanel>("ResultRows").Children.OfType<Border>().Count());
            Named<ComboBox>("ResultsClassFilter").SelectedIndex = 1;
            window.UpdateLayout();
            Assert.Single(Named<StackPanel>("ResultRows").Children.OfType<Border>());
            ClickNamed("ResultsResetFilters");
            Assert.Equal(2, Named<StackPanel>("ResultRows").Children.OfType<Border>().Count());

            ClickNamed("ResultOpen_demo-001");
            ClickNamed("ChoiceRemove_BIO_LF");
            Assert.True(Named<Button>("ChoiceSave").IsEnabled);
            ClickNamed("ChoiceSave");
            Assert.True(Named<Border>("ChoiceEditorError").IsVisible);

            // Planning: legend and follow-up instructions start collapsed
            Click("Kursplanung");
            Assert.False(Named<Expander>("PlanningLegend").IsExpanded);
            Assert.False(Named<Expander>("PlanningFollowUp").IsExpanded);

            // Reconciliation: name filter + reset
            Click("ASV-Abgleich");
            window.LoadAsvComparisonBytes(System.Text.Encoding.UTF8.GetBytes(",Name mit Vornamen,ASV-ID,Fächer Kursstufe\nKS1,\"Eins, Demo\",demo-001,\"D [5];1-L-X-X-X-X\"\nKS1,\"Zwei, Demo\",demo-002,\"D [5];1-L-X-X-X-X\""), "asv-test.csv");
            window.UpdateLayout();
            var summary = Named<TextBlock>("AsvSummary");
            Assert.Contains("2 Abweichungen", summary.Text);
            Assert.Equal(2, Named<StackPanel>("AsvResultRows").Children.OfType<Border>().Count());
            Named<TextBox>("AsvSearch").Text = "Demo Zwei";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Single(Named<StackPanel>("AsvResultRows").Children.OfType<Border>());
            Assert.Contains(Named<StackPanel>("AsvResultRows").GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text?.Contains("Demo Zwei") == true);
            ClickNamed("AsvResetFilters");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(2, Named<StackPanel>("AsvResultRows").Children.OfType<Border>().Count());

            // Course annotations: success status containing "bitte" must NOT be styled red as error
            Click("Einstellungen");
            Click("Jahrgang verwalten");
            Click("Fachangebot ansehen / für Schülerprogramm exportieren");
            Named<Expander>("CourseAnnotations").IsExpanded = true;
            window.UpdateLayout();
            Named<ComboBox>("Coop_CH_LF").SelectedIndex = 1;
            window.UpdateLayout();
            Assert.True(alertBanner.IsVisible);
            var alertText = string.Join(" ", alertBanner.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));
            Assert.Contains("bitte", alertText, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(Avalonia.Media.Color.Parse("#A32D2D"), ((Avalonia.Media.ISolidColorBrush)alertBanner.BorderBrush!).Color);
            Assert.NotEqual(Avalonia.Media.Color.Parse("#FCEBEB"), ((Avalonia.Media.ISolidColorBrush)alertBanner.Background!).Color);
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [AvaloniaFact]
    public void PedagogicalNetworkFlowShowsHintsCreatesBundleAndReopensPackageWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-paed-flow-" + Guid.NewGuid());
        var sourceRoot = Path.Combine(root, "source");
        TeacherWindow? sourceWindow = null;
        TeacherWindow? pkgWindow = null;
        TeacherWindow? reopenedWindow = null;
        try
        {
            var workspace = new TeacherWorkspace(sourceRoot);
            workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
            workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
            using (var db = workspace.OpenActive())
            {
                db.SaveStudent(new(SchemaVersions.StudentRecord,
                    new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
                    "", "", "10a/Eins.Demo", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            }

            sourceWindow = new TeacherWindow(sourceRoot) { Width = 1400, Height = 900 };
            sourceWindow.Show();
            sourceWindow.UpdateLayout();

            IEnumerable<T> AllIn<T>(TeacherWindow w) where T : Control => w.GetLogicalDescendants().OfType<T>().Distinct();
            T NamedIn<T>(TeacherWindow w, string name) where T : Control => AllIn<T>(w).Single(c => c.Name == name);
            void ClickIn(TeacherWindow w, string label)
            {
                AllIn<Button>(w).Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                w.UpdateLayout();
            }

            ClickIn(sourceWindow, "Wahl");
            ClickIn(sourceWindow, "Schülerdateien verteilen");
            Assert.Contains(AllIn<TextBlock>(sourceWindow), t => t.Text?.Contains("im pädagogischen Netz") == true);
            Assert.True(NamedIn<Expander>(sourceWindow, "TransferOutExpander").IsExpanded);
            Assert.Contains(AllIn<Button>(sourceWindow), b => b.Name == "ExportTransferBundle");
            Assert.Contains(AllIn<Button>(sourceWindow), b => b.Content?.ToString() == "Stand für pädagogisches Netz speichern …");

            ClickIn(sourceWindow, "Wahl");
            ClickIn(sourceWindow, "Kurswahlen einsammeln");
            var cleanupHint = NamedIn<Border>(sourceWindow, "PostCollectionCleanupHint");
            Assert.Contains(cleanupHint.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("löschen") == true);
            var backExpander = NamedIn<Expander>(sourceWindow, "TransferBackExpander");
            Assert.Contains(backExpander.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("löschen") == true);

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            sourceWindow.Close();
            sourceWindow = null;
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }

            var teacherDir = Path.Combine(root, "TeacherApp");
            Directory.CreateDirectory(teacherDir);
            File.WriteAllText(Path.Combine(teacherDir, "GsmKurswahl.TeacherApp.exe"), "synthetic-exe");
            File.WriteAllText(Path.Combine(teacherDir, TeacherProgramLease.FileName), "held");

            var studentDir = Path.Combine(root, "StudentApp");
            Directory.CreateDirectory(studentDir);
            File.WriteAllText(Path.Combine(studentDir, "GsmKurswahl.StudentApp.exe"), "synthetic-student");
            File.WriteAllText(Path.Combine(studentDir, "hostfxr.dll"), "synthetic-hostfxr");
            File.WriteAllText(Path.Combine(studentDir, "GsmKurswahl.StudentApp.runtimeconfig.json"),
                """{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"}]}}""");

            var pkgZip = Path.Combine(root, "paket.zip");
            using (var db = new TeacherWorkspace(sourceRoot).OpenActive())
            {
                PedagogicalTransferPackage.Export(new TeacherWorkspace(sourceRoot).Active!, db, pkgZip, teacherDir, studentDir);
            }

            var extracted = Path.Combine(root, "extracted");
            System.IO.Compression.ZipFile.ExtractToDirectory(pkgZip, extracted);
            var pkgWorkspaceRoot = Path.Combine(extracted, PedagogicalTransferPackage.WorkspaceFolderName);

            pkgWindow = new TeacherWindow(pkgWorkspaceRoot, extracted) { Width = 1400, Height = 900 };
            pkgWindow.Show();
            pkgWindow.UpdateLayout();
            Assert.Contains(AllIn<TextBlock>(pkgWindow), t => t.Text == "Transportstand prüfen");

            ClickIn(pkgWindow, "Transportstand übernehmen und öffnen");
            Assert.True(File.Exists(Path.Combine(pkgWorkspaceRoot, "verwaltung.db")));
            Assert.Contains(AllIn<TextBlock>(pkgWindow), t => t.Text?.Contains("3 · Schülerdateien verteilen (im pädagogischen Netz)") == true);
            Assert.False(NamedIn<Expander>(pkgWindow, "TransferOutExpander").IsExpanded);

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            pkgWindow.Close();
            pkgWindow = null;
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }

            reopenedWindow = new TeacherWindow(pkgWorkspaceRoot, extracted) { Width = 1400, Height = 900 };
            reopenedWindow.Show();
            reopenedWindow.UpdateLayout();
            Assert.Contains(AllIn<TextBlock>(reopenedWindow), t => t.Text == "Ihr aktueller Jahrgang");
            Assert.DoesNotContain(AllIn<TextBlock>(reopenedWindow), t => t.Text == "Transportstand prüfen");
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            sourceWindow?.Close();
            pkgWindow?.Close();
            reopenedWindow?.Close();
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
