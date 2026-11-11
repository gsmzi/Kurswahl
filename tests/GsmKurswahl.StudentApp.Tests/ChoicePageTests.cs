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

// Nutzerauftrag 11.11.2026: after collection all choices are handled inside the teacher program.
public sealed class ChoicePageTests
{
    [AvaloniaFact]
    public void TeacherFiltersEditsAndCancelsCoursesInsideTheTeacherProgram()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-choices-" + Guid.NewGuid());
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        try
        {
            DemoWorkspace.Prepare(root, catalog);
            var window = new TeacherWindow(root) { Width = 1400, Height = 900 };
            try
            {
                window.Show(); window.UpdateLayout();
                IEnumerable<T> All<T>() where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
                T Named<T>(string name) where T : Control => All<T>().Single(c => c.Name == name);
                bool Exists<T>(string name) where T : Control => All<T>().Any(c => c.Name == name);
                void Click(string label) { All<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
                void ClickNamed(string name) { Named<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
                bool Failed() => All<TextBlock>().Any(t => t.Text?.Contains("Vorgang nicht abgeschlossen") == true);
                void Capture(string name)
                {
                    if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is not { Length: > 0 } directory) return;
                    Directory.CreateDirectory(directory); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(directory, name + ".png"));
                }
                int Rows() => Named<StackPanel>("ResultRows").Children.OfType<Border>().Count();

                Click("Kurswahlen");
                Assert.Equal(80, Rows());
                Capture("kurswahlen-liste");
                // Former classes are part of the stock: four classes of twenty.
                var classes = Named<ComboBox>("ResultsClassFilter");
                Assert.Equal(5, classes.ItemCount);
                classes.SelectedIndex = 1; window.UpdateLayout();
                Assert.Equal(20, Rows());
                classes.SelectedIndex = 0;
                // Course filter: everyone attending psychology; with the semester the list of HJ 3 is empty (PSY is HJ 1+2).
                var course = Named<ComboBox>("ResultsCourseFilter");
                course.SelectedItem = course.Items.Cast<object>().Single(i => i.ToString() == "Psychologie Wahlfach"); window.UpdateLayout();
                var psychology = Rows(); Assert.InRange(psychology, 1, 79);
                Named<ComboBox>("ResultsSemesterFilter").SelectedIndex = 3; window.UpdateLayout();
                Assert.Equal(0, Rows());
                Named<ComboBox>("ResultsSemesterFilter").SelectedIndex = 0; course.SelectedIndex = 0; window.UpdateLayout();
                foreach (var print in new[] { "PrintOverview", "PrintControlSheets", "PrintCourseLists" }) { ClickNamed(print); Assert.False(Failed(), print); }

                // Open one pupil: the grid shows the choice; removing history is a rule violation (red), not a silent change.
                ClickNamed("ResultOpen_demo-001");
                Assert.True(Exists<TextBlock>("ChoiceEditorTitle"));
                Assert.False(Named<Button>("ChoiceSave").IsEnabled);
                ClickNamed("ChoiceRemove_G_BF");
                Assert.True(Exists<Border>("ChoiceViolations"));
                Assert.True(Named<Button>("ChoiceSave").IsEnabled);
                Capture("kurswahlen-bearbeiten-rot");
                Named<TextBox>("ChoiceReason").Text = "Synthetische Teständerung";
                // Without the explicit confirmation a violating choice is not stored.
                ClickNamed("ChoiceSave");
                Assert.True(Failed());
                Assert.DoesNotContain("Regelverstoß", Named<TextBlock>("ResultReview_demo-001").Text);
                Named<CheckBox>("ChoiceForce").IsChecked = true;
                ClickNamed("ChoiceSave");
                Assert.Contains("Regelverstoß", Named<TextBlock>("ResultReview_demo-001").Text);
                Assert.Contains("Nachwahl", Named<TextBlock>("ResultReview_demo-001").Text);
                // The pupil now shows up under "Nachwahl offen" and "Unvollständige Kurswahl"; the re-election sheet can be printed.
                Named<ComboBox>("ResultsStatusFilter").SelectedIndex = (int)ChoiceStateFilter.ReselectionOpen; window.UpdateLayout();
                Assert.Equal(1, Rows());
                ClickNamed("PrintReselections"); Assert.DoesNotContain(All<TextBlock>(), t => t.Text?.Contains("keine Nachwahl offen") == true);
                Named<ComboBox>("ResultsStatusFilter").SelectedIndex = (int)ChoiceStateFilter.Incomplete; window.UpdateLayout();
                Assert.Equal(1, Rows());
                // Enter the re-election directly: add history again, save, the re-election is closed.
                var add = Named<ComboBox>("ChoiceAdd");
                add.SelectedItem = add.Items.Cast<object>().Single(i => i.ToString()!.StartsWith("Geschichte BF")); window.UpdateLayout();
                Assert.False(Exists<Border>("ChoiceViolations"));
                Assert.True(Named<CheckBox>("ChoiceCloseReselection").IsVisible);
                Named<TextBox>("ChoiceReason").Text = "Nachwahl eingetragen";
                ClickNamed("ChoiceSave");
                Named<ComboBox>("ResultsStatusFilter").SelectedIndex = 0; window.UpdateLayout();
                Assert.DoesNotContain("Nachwahl", Named<TextBlock>("ResultReview_demo-001").Text);
                Assert.DoesNotContain("Regelverstoß", Named<TextBlock>("ResultReview_demo-001").Text);
                Capture("kurswahlen-bearbeiten");
                ClickNamed("ChoiceClose");
                Assert.False(Named<ContentControl>("ChoiceDetail").IsVisible);

                // Cancel a course straight from the demand table: preview first, then removed for everyone.
                Click("Kursplanung");
                ClickNamed("CancelOffer_PSY_WF");
                Assert.True(Exists<Button>("CancelConfirm"));
                Capture("kursplanung-streichen");
                ClickNamed("CancelConfirm");
                Assert.False(Failed());
                Assert.False(Exists<Button>("CancelOffer_PSY_WF"));
                Click("Kurswahlen");
                Assert.DoesNotContain(Named<ComboBox>("ResultsCourseFilter").Items.Cast<object>(), i => i.ToString() == "Psychologie Wahlfach");
            }
            finally { window.Close(); }
            // The stock itself: original pupil choice kept, teacher edits and the cancellation appended as new revisions.
            var workspace = new TeacherWorkspace(root);
            using var store = workspace.OpenActive();
            var history = store.History("demo-001");
            Assert.Equal(RevisionSource.StudentSubmission, history[0].Source);
            Assert.Equal(2, history.Count(r => r.Source == RevisionSource.TeacherEdit));
            Assert.Contains(history[0].Snapshot.Selections, s => s.OfferId == "G-BF");
            Assert.Equal(OfferStatus.Cancelled, workspace.Active!.Catalog.Offers.Single(o => o.Id == "PSY-WF").Status);
            Assert.DoesNotContain(store.Students().Select(s => store.History(s.Student.ExternalStudentId).Last()), r => r.Snapshot.Selections.Any(s => s.OfferId == "PSY-WF"));
            Assert.Contains(store.PlanningDocuments(), d => d.Name.StartsWith("Vor Streichung Psychologie Wahlfach"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void FinallyCollectedPupilsAreNotReadAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsm-final-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            using var store = new SqliteAdministrationStore(Path.Combine(root, "verwaltung.db"));
            Assert.False(TeacherWindow.FinallyCollected(store, "demo-001"));
            store.ArchiveCollectedFiles([new CollectedFile(1, Guid.NewGuid(), "demo-001", DateTimeOffset.UtcNow, "demo-beratung", "aktuell.json",
                StudentFolderCleanup.Token([1, 2, 3]), [1, 2, 3])]);
            Assert.True(TeacherWindow.FinallyCollected(store, "demo-001"));
            Assert.False(TeacherWindow.FinallyCollected(store, "demo-002"));
        }
        finally { Directory.Delete(root, true); }
    }
}
