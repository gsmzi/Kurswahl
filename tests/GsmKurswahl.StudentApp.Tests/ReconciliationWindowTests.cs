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

public sealed class ReconciliationWindowTests
{
    [AvaloniaFact]
    public void NavigationAndExplicitReceiptActivationWorkWithoutChangingChoices()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-asv-ui-" + Guid.NewGuid());
        var workspace = new TeacherWorkspace(root); workspace.Create(TestData.WorkflowCatalog, "demo-test");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var store = workspace.OpenActive())
        {
            store.EnablePlanning();
            store.SaveStudent(new(1, new(Guid.NewGuid(), "demo-001", "Demo Abschluss", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
            new TeacherSession(store, TestData.WorkflowCatalog, "demo-test").Save(TestData.Choice, null, RevisionSource.TeacherEdit, "Demo Ursprung");
        }
        var window = new TeacherWindow(root);
        try
        {
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            window.Show(); Click("Abgleich / Abschluss");
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("needs_review/invalid") == true);
            var note = window.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Watermark == "Bearbeitungsvermerk / Ausnahmegrund");
            note.Text = "Synthetischer Probelauf"; Click("Manuelle ASV-Eingabe dokumentieren");
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("manuell_uebertragen") == true);
            Click("Kursplanung"); Click("Abgleich / Abschluss");
            if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } screenshots)
            {
                Directory.CreateDirectory(screenshots); window.UpdateLayout();
                using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(screenshots, "teacher-m5.png"));
            }
            window.Close();
            using var result = workspace.OpenActive(); Assert.Single(result.TransferReceipts()); Assert.Single(result.History("demo-001"));
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
}
