using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GsmKurswahl.AdministrationStorage;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.TeacherApp;
using GsmKurswahl.Tests;

namespace GsmKurswahl.StudentApp.Tests;

public sealed class ReconciliationWindowTests
{
    [AvaloniaFact]
    public void AsvComparisonChecksCongruenceFiltersAndPreservesHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-asv-ui-" + Guid.NewGuid());
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects],
            Offers =
            [
                .. TestData.WorkflowCatalog.Offers,
                new("BIO-LF-BILI", "BIO", CourseType.Lf, CourseVariant.Bilingual, OfferStatus.Offered,
                    [new(1, 5), new(2, 5), new(3, 5), new(4, 5)], [Requirement.ContinuousBilingual])
            ]
        };
        var workspace = new TeacherWorkspace(root); workspace.Create(catalog, "demo-test");
        workspace.Update(workspace.Active! with { Step = YearSetupStep.Complete });
        using (var store = workspace.OpenActive())
        {
            store.EnablePlanning();
            store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Demo Eins", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
            store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-002", "Demo Zwei", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);
            store.SaveStudent(new(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-003", "Demo Drei", "10b", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null), null);

            var session = new TeacherSession(store, catalog, "demo-test");
            session.Save(TestData.Choice with
            {
                StudentId = "demo-001",
                Selections =
                [
                    TestData.Select(catalog.Offers.Single(o => o.Id == "D-LF")),
                    TestData.Select(catalog.Offers.Single(o => o.Id == "BIO-LF-BILI")),
                    TestData.Select(catalog.Offers.Single(o => o.Id == "M-LF")),
                    TestData.Select(catalog.Offers.Single(o => o.Id == "G-BF"))
                ],
                PlannedOralSubjects = ["G"]
            }, null, RevisionSource.TeacherEdit, "Demo Eins");

            session.Save(TestData.Choice with
            {
                StudentId = "demo-002",
                Selections =
                [
                    TestData.Select(catalog.Offers.Single(o => o.Id == "D-LF")),
                    new("GK-BF", "GK", CourseType.Bf, CourseVariant.Normal, [1, 2])
                ],
                PlannedOralSubjects = ["D"]
            }, null, RevisionSource.TeacherEdit, "Demo Zwei");
        }

        var window = new TeacherWindow(root);
        try
        {
            T Named<T>(string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct().Single(c => c.Name == name);
            void Click(string label)
            {
                window.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }
            void ClickNamed(string name)
            {
                Named<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
            }

            window.Show(); Click("ASV-Abgleich");
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Name == "AsvReceiptSummary");
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<Button>(), b => b.Content?.ToString() == "Manuelle ASV-Eingabe dokumentieren");

            var csv =
                ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\n" +
                "KS1,\"Eins, Demo\",demo-001,\"D [5];1-L-X-X-X-X;BIO [5];2-L-X-X-X-X;M [5];3-L-X-X-X-X;G [2];m1-p-X-X-X-X\"\n" +
                "KS1,\"Zwei, Demo\",demo-002,\"D [5];1-L-X-X-X-X;GK [2];m1-P-X-X-Z-Z\"\n" +
                "KS1,\"Vier, Demo\",demo-004,\"D [5];1-L-X-X-X-X\"";

            window.LoadAsvComparisonBytes(Encoding.UTF8.GetBytes(csv), "asv-demo.csv");
            window.UpdateLayout();

            var summary = Named<TextBlock>("AsvSummary").Text ?? "";
            Assert.Contains("1 deckungsgleich", summary);
            Assert.Contains("1 Abweichungen", summary);
            Assert.Contains("1 nur in GSM-Kurswahl", summary);
            Assert.Contains("1 nur in ASV", summary);

            // Default filter ("Nur Abweichungen & Auffälligkeiten") hides congruent demo-001 -> 3 cards
            var resultRows = Named<StackPanel>("AsvResultRows");
            Assert.Equal(3, resultRows.Children.OfType<Border>().Count());
            Assert.Contains(resultRows.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Mündliche Prüfungsfächer") == true);

            // Switch to "Alle" -> all 4 cards including Bili note on demo-001
            Named<ComboBox>("AsvKindFilter").SelectedIndex = 1;
            window.UpdateLayout();
            Assert.Equal(4, resultRows.Children.OfType<Border>().Count());
            Assert.Contains(resultRows.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("bilingual") == true);

            // Search and reset
            Named<TextBox>("AsvSearch").Text = "Demo Zwei";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Single(resultRows.Children.OfType<Border>());
            ClickNamed("AsvResetFilters");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(3, resultRows.Children.OfType<Border>().Count());

            if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is { Length: > 0 } screenshots)
            {
                Directory.CreateDirectory(screenshots); window.UpdateLayout();
                using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(screenshots, "teacher-m5.png"));
            }

            // Jump to Kurswahlen for demo-002
            ClickNamed("AsvOpenChoice_demo-002");
            Assert.Contains(window.GetLogicalDescendants().OfType<Grid>(), g => g.Name == "ChoicePage");

            window.Close();
            using var result = workspace.OpenActive();
            Assert.Single(result.History("demo-001"));
            Assert.Single(result.History("demo-002"));
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
}
