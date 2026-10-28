using System.Reflection;
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

public sealed class PlanningWindowTests
{
    [AvaloniaFact]
    public void PlanningPreviewRequiresConfirmationAndReopensCommittedCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-planning-ui-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-beratung");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var store = workspace.OpenActive())
        {
            store.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-beratung", DateTimeOffset.UtcNow, null), null);
            new TeacherSession(store, TestData.WorkflowCatalog, "demo-beratung").Save(TestData.Choice with
            { Selections = [TestData.Select(TestData.WorkflowCatalog.Offers.Single(o => o.Id == "BIO-LF"))] }, null, RevisionSource.StudentSubmission, "Demo Abgabe");
        }
        var window = new TeacherWindow(root);
        try
        {
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            T Control<T>(string name) where T : Avalonia.Controls.Control => window.GetLogicalDescendants().OfType<T>().Distinct().Single(c => c.Name == name);
            window.Show(); Click("Kursplanung"); Click("Kursplanung aktivieren · Datenbankschema 2");
            var store = (SqliteAdministrationStore)typeof(TeacherWindow).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Assert.True(store.PlanningEnabled); Control<TabControl>("PlanningTabs").SelectedIndex = 2;
            Control<CheckBox>("PlanningCancel_BIO_LF").IsChecked = true; Click("Streichung nur simulieren");
            Assert.Single(store.History("demo-001"));
            Control<TextBox>("PlanningReason").Text = "Demo Kurs kommt nicht zustande";
            Click("Geprüfte Angebotsänderung im Lehrerbestand übernehmen"); Assert.Single(store.History("demo-001"));
            Control<CheckBox>("PlanningConfirm").IsChecked = true;
            Click("Geprüfte Angebotsänderung im Lehrerbestand übernehmen");
            Assert.Equal(2, store.History("demo-001").Length);
            Assert.Equal(OfferStatus.Cancelled, new TeacherWorkspace(root).Active!.Catalog.Offers.Single(o => o.Id == "BIO-LF").Status);
            if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } screenshots)
            {
                Directory.CreateDirectory(screenshots); window.UpdateLayout();
                using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(screenshots, "teacher-planning.png"));
            }
            window.Close();
            // Simulate a crash after the SQLite commit but before the workspace copy was synchronized.
            var staleWorkspace = new TeacherWorkspace(root);
            staleWorkspace.Update(staleWorkspace.Active! with { Catalog = TestData.WorkflowCatalog });
            window = new TeacherWindow(root); window.Show(); Click("Kursplanung");
            Assert.Equal(OfferStatus.Cancelled, new TeacherWorkspace(root).Active!.Catalog.Offers.Single(o => o.Id == "BIO-LF").Status);
            Control<TabControl>("PlanningTabs").SelectedIndex = 2; Assert.DoesNotContain(window.GetLogicalDescendants().OfType<CheckBox>(), c => c.Name == "PlanningCancel_BIO_LF");
            Click("Wahlergebnisse"); Click("Kursplanung"); Click("Hilfe"); Click("Kursplanung");
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
}
