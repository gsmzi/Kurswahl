using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;
using GsmKurswahl.TeacherApp;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class DemoWorkspaceTests
{
    [AvaloniaFact]
    public void EightyChoicesAreStructurallyValidDiverseAndReopeningPreservesEdits()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-demo80-" + Guid.NewGuid());
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        try
        {
            DemoWorkspace.Prepare(root, catalog);
            var workspace = new TeacherWorkspace(root);
            Assert.Equal(YearSetupStep.Complete, workspace.Active!.Step);
            using (var store = workspace.OpenActive())
            {
                var session = new TeacherSession(store, catalog, "demo-test");
                var states = session.Overview(); Assert.Equal(80, states.Length);
                Assert.All(states, s => { Assert.NotNull(s.Active); Assert.Equal(ReviewStatus.NeedsReview, s.Validation!.Status);
                    Assert.DoesNotContain(s.Validation.Messages, m => m.Rule.Severity == ReviewStatus.Invalid);
                    Assert.False(s.RequiresReselection); Assert.Equal(3, s.Active!.Snapshot.Selections.Count(c => c.Type == CourseType.Lf));
                    Assert.Equal(SubmissionStatus.Draft, s.Active.Snapshot.SubmissionStatus);
                    Assert.True(File.Exists(Path.Combine(s.Record.HomeDirectory, "profil.json"))); });
                Assert.Equal(4, states.Select(s => s.Record.Student.ClassName).Distinct().Count());
                var selections = states.SelectMany(s => s.Active!.Snapshot.Selections).ToArray();
                foreach (var semesters in new[] { new[] { 1 }, new[] { 1, 2 }, new[] { 1, 3, 4 }, new[] { 1, 2, 3, 4 } })
                    Assert.Contains(selections, s => s.OfferId == "GK-BF" && s.Semesters.SequenceEqual(semesters));
                Assert.Contains(selections, s => s.OfferId == "GEO-BF" && s.Semesters.SequenceEqual(new[] { 3 }));
                var snapshot = store.PlanningDocuments().First(); var analysis = CoursePlanning.Analyze(snapshot);
                Assert.Equal(76, analysis.Relevant); Assert.Equal(68, CoursePlanning.Analyze(snapshot with { IncludeExpectedDepartures = false }).Relevant);
                if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } pdfFolder)
                {
                    Directory.CreateDirectory(pdfFolder);
                    File.WriteAllBytes(Path.Combine(pdfFolder, "wahlzahlen.pdf"), GsmKurswahl.ImportExport.PlanningPrint.Demand(2029, snapshot.Name, "Mit voraussichtlichen Abgängern", PlanningPresentation.DemandTable(snapshot)));
                    var preview = CoursePlanning.PreviewCancellation(snapshot, ["BIO-LF-BILI"]);
                    var items = preview.Changes.Where(c => !c.RemovedOffers.IsEmpty).Select(c => new GsmKurswahl.ImportExport.ReselectionPrintItem(
                        states.Single(s => s.Record.Student.ExternalStudentId == c.StudentId).Record, c.Choice, c.RemovedOffers.ToArray())).ToArray();
                    File.WriteAllBytes(Path.Combine(pdfFolder, "nachwahl-gesamt.pdf"), GsmKurswahl.ImportExport.PlanningPrint.Reselections(items, preview.Catalog));
                    File.WriteAllBytes(Path.Combine(pdfFolder, "nachwahl-gesamt.docx"), GsmKurswahl.ImportExport.ReselectionWord.Render(items, preview.Catalog));
                }
                Assert.Equal(16, analysis.Combinations.Length); Assert.NotEmpty(analysis.Overlaps);
                foreach (var id in new[] { "BIO-LF-BILI", "BIO-BF-BILI", "INF-BF", "PSY-WF", "SIA-SK", "F-LF", "L-LF" })
                    Assert.Contains(analysis.Demand, d => d.OfferId == id && d.All > 0);
                Assert.Equal(80, TeacherDashboardBuilder.Build(store, session).Delivered);
                var r = states[0].Record;
                session.Save(states[0].Active!.Snapshot, states[0].Active!.Id, RevisionSource.TeacherEdit, "Demo Teständerung");
            }
            DemoWorkspace.Prepare(root, catalog);
            using (var reopened = workspace.OpenActive())
            {
                Assert.Equal(2, reopened.History("demo-001").Length);
                Assert.Equal(3, reopened.PlanningDocuments().Length);
            }
            var window = new TeacherWindow(root);
            try
            {
                window.Show(); window.UpdateLayout();
                void Click(string label) { window.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
                T Control<T>(string name) where T : Avalonia.Controls.Control => window.GetLogicalDescendants().OfType<T>().Distinct().Single(c => c.Name == name);
                Click("Wahl"); Assert.True(Control<StackPanel>("WorkflowProgress").IsVisible);
                Click("Kursplanung"); Assert.False(Control<StackPanel>("WorkflowProgress").IsVisible);
                window.UpdateLayout();
                Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("80 Personen") == true);
                Assert.Equal(catalog.Subjects.Length, Control<Grid>("PlanningMatrix").GetLogicalDescendants().OfType<Border>().Count(b => b.Name?.StartsWith("PlanningSubject_") == true));
                Control<ComboBox>("PlanningIncludeDepartures").SelectedIndex = 1;
                Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("68 planungsrelevant") == true);
                window.UpdateLayout();
                if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } screenshots)
                {
                    Directory.CreateDirectory(screenshots);
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(screenshots, "demo-80-analysis.png"));
                }
                window.Width = 900; window.Height = 650; window.UpdateLayout();
                Assert.Single(Control<Grid>("PlanningMatrix").Children);
                if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } narrowScreenshots)
                {
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(narrowScreenshots, "demo-80-analysis-narrow.png"));
                }
                window.Width = 1400; window.Height = 900; window.UpdateLayout();
                Click("Wahlergebnisse"); Assert.False(Control<StackPanel>("WorkflowProgress").IsVisible);
                Assert.Equal(80, Control<StackPanel>("ResultRows").Children.Count);
                Assert.DoesNotContain(window.GetLogicalDescendants().OfType<ComboBox>(), c => c.Name == "PrereqImp");
                Control<ComboBox>("ResultDeparture_demo-002").SelectedIndex = 2;
                if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } resultScreenshots)
                {
                    window.UpdateLayout(); using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(resultScreenshots, "demo-80-results.png"));
                }
                Click("Kursplanung");
                Assert.True(window.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("67 planungsrelevant") == true), "Filter=" + Control<ComboBox>("PlanningIncludeDepartures").SelectedIndex + " / " + string.Join(" | ", window.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => t?.Contains("planungsrelevant") == true)));
                Control<ComboBox>("PlanningIncludeDepartures").SelectedIndex = 0;
                Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("75 planungsrelevant") == true);
                Control<TabControl>("PlanningTabs").SelectedIndex = 2; window.UpdateLayout();
                Control<CheckBox>("PlanningCancel_BIO_LF_BILI").IsChecked = true; Click("Streichung nur simulieren");
                if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } impactScreenshots)
                {
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(impactScreenshots, "streichung-vorschau.png"));
                }
                Click("Wahl"); Click("Schülerdateien verteilen");
                Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), b => b.Content?.ToString() == "Stand für pädagogisches Netz speichern …");
                Click("Wahl"); Click("Kurswahlen einsammeln");
                Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), b => b.Content?.ToString() == "Stand für Verwaltungsnetz speichern …");
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void ExistingUnmarkedDirectoryIsNeverOverwritten()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-demo80-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "existing.txt"), "keep");
            Assert.Throws<IOException>(() => DemoWorkspace.Prepare(root, GsmKurswahl.Tests.TestData.WorkflowCatalog));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "existing.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
}
