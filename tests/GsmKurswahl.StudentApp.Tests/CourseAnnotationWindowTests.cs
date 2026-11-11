using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.Tests;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class CourseAnnotationWindowTests
{
    [AvaloniaFact]
    public void TeacherMarksCoursesAfterCancellationAndReopensWithoutChoiceChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-annotations-ui-" + Guid.NewGuid());
        TeacherWindow? window = null;
        try
        {
            var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
            string history;
            using (var store = workspace.OpenActive())
            {
                store.SaveStudent(new StudentRecord(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Muster, Demo", "10a", 2029,
                    DepartureStatus.Active, PriorEducation.Unknown), "", "", "", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
                new TeacherSession(store, workspace.Active!.Catalog, "demo-beratung").Save(TestData.Choice with
                { Selections = [TestData.Select(TestData.WorkflowCatalog.Offers.Single(o => o.Id == "BIO-LF"))] }, null, RevisionSource.StudentSubmission, "Demo-Abgabe");
                var preview = CoursePlanning.PreviewCancellation(CoursePlanning.Capture(store, workspace.Active.Catalog, "demo-beratung", "Test", true), ["BIO-LF"]);
                CoursePlanning.Confirm(store, preview, "demo-beratung", "Teststreichung", true);
                workspace.Update(workspace.Active with { Catalog = preview.Catalog, Step = YearSetupStep.Complete });
                history = JsonSerializer.Serialize(store.History("demo-001"), DocumentJson.Options);
            }
            window = new TeacherWindow(root) { Width = 1400, Height = 900 }; window.Show(); window.UpdateLayout();
            T Named<T>(string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
            void Click(string label) { window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
            void OpenCatalog() { Click("Einstellungen"); Click("Jahrgang verwalten"); Click("Fachangebot ansehen / für Schülerprogramm exportieren"); Named<Expander>("CourseAnnotations").IsExpanded = true; window.UpdateLayout(); }
            OpenCatalog();
            Named<ComboBox>("Coop_CH_LF").SelectedIndex = 1;
            Named<CheckBox>("TopUp_CH_LF").IsChecked = true;
            Named<ComboBox>("Coop_E_LF").SelectedIndex = 2;
            window.UpdateLayout();
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("nicht vollständig gespeichert") == true);
            if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory); Named<Expander>("CourseAnnotations").BringIntoView(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(directory, "kursmarkierungen.png"));
            }
            window.Close(); window = new TeacherWindow(root); window.Show(); window.UpdateLayout(); OpenCatalog();
            Assert.Equal(1, Named<ComboBox>("Coop_CH_LF").SelectedIndex); Assert.True(Named<CheckBox>("TopUp_CH_LF").IsChecked);
            Assert.Equal(2, Named<ComboBox>("Coop_E_LF").SelectedIndex);
            window.Close(); window = null;
            workspace = new TeacherWorkspace(root);
            Assert.Equal(OfferStatus.Cancelled, workspace.Active!.Catalog.Offers.Single(o => o.Id == "BIO-LF").Status);
            using var reopened = workspace.OpenActive();
            Assert.Equal(history, JsonSerializer.Serialize(reopened.History("demo-001"), DocumentJson.Options));
            Assert.True(reopened.Reselection("demo-001").Required);
        }
        finally { window?.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
