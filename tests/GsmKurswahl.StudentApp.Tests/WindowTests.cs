using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;
using GsmKurswahl.Tests;

[assembly: AvaloniaTestApplication(typeof(GsmKurswahl.StudentApp.Tests.TestAppBuilder))]

namespace GsmKurswahl.StudentApp.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
public sealed class TestApp : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public class WindowTests
{
    [AvaloniaFact]
    public async Task GuidedWorkflowSaveAndReloadWorkWithActualJsonStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gsm-ui-" + Guid.NewGuid());
        var path = Path.Combine(directory, "aktuell.json");
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", new JsonChoiceStore(path));
        var window = new MainWindow(session, path, "demo-account");
        try
        {
            window.Show();
            ToggleButton Box(string name) => window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == name);
            bool Has(string name) => window.GetVisualDescendants().OfType<Control>().Any(b => b.Name == name);
            var next = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextStep");
            Assert.False(next.IsEnabled);
            foreach (var subject in new[] { "E", "BIO", "W" }) Box("Offer_" + subject + "_LF").IsChecked = true;
            Assert.True(next.IsEnabled);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // D/M are forced and shown as fixed facts, not as disabled checkboxes.
            Assert.Equal(new[] { "D", "M" }, session.Choice.PlannedOralSubjects);
            Assert.False(Has("Oral_D"));
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("ist gesetzt") == true);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(3, session.Step);
            // Only open decisions are asked; required courses are not offered as controls.
            Assert.False(Has("Offer_G_BF"));
            Assert.IsType<RadioButton>(Box("Option_BK_BF"));
            Box("Option_BK_BF").IsChecked = true;
            // The answered question collapses and the next open one follows; "Ändern" reopens it.
            Assert.False(Has("Option_MU_BF"));
            Assert.True(Has("Option_ETH_BF"));
            window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "Task_PLAN_AREA_ART").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Box("Option_MU_BF").IsChecked = true;
            window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "TaskDone").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "BK");
            Box("Option_ETH_BF").IsChecked = true;
            Box("Option_CH_BF").IsChecked = true;
            Assert.All(session.Tasks, t => Assert.True(t.IsSatisfied));
            Assert.True(Has("Plan_0"));
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(4, session.Step);
            Assert.False(Box("Sem_GK_BF_1").IsEnabled);
            Box("Sem_GK_BF_2").IsChecked = true;
            Assert.Equal(new[] { 1, 2 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Box("Offer_BK_BF").IsChecked = true;
            Assert.Contains(session.Choice.Selections, s => s.SubjectId == "BK");
            Assert.True(Has("Offer_BK_BF"));
            var save = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SaveButton");
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var i = 0; i < 200 && session.IsBusy; i++) await Task.Delay(10);
            Assert.False(session.IsBusy);
            Assert.False(session.IsDirty);
            Assert.True(File.Exists(path));
            var second = new StudentSession(TestData.WorkflowCatalog, "demo-001", new JsonChoiceStore(path));
            await second.LoadAsync();
            Assert.Equal(session.Choice.Selections.Length, second.Choice.Selections.Length);
            Assert.Equal(new[] { "D", "M" }, second.Choice.PlannedOralSubjects);
            Assert.Equal(ReviewStatus.NeedsReview, second.Validation.Status);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("nicht eingereicht") == true);
        }
        finally { window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public void RestrictedOfferIsHiddenAndReasonAvailable()
    {
        var offer = TestData.Offer("D-LF", type: CourseType.Lf) with { Variant = CourseVariant.Bilingual, Requirements = [Requirement.ContinuousBilingual] };
        var window = new MainWindow(new StudentSession(TestData.Catalog with { Offers = [offer] }, "demo-001", new EmptyStore()), "demo.json", "demo-account");
        try
        {
            window.Show();
            // Restricted offers are not shown as options, but their reason stays reachable in a collapsed list.
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<CheckBox>(), b => b.Name == "Offer_D_LF");
            var restricted = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Name != "Prerequisites");
            Assert.Contains(restricted.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Persönliche Voraussetzung ungeprüft") == true);
            // Compact footer information stays available, read-only and initially collapsed.
            var prerequisites = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Name == "Prerequisites");
            Assert.False(prerequisites.IsExpanded);
            Assert.Contains("ungeprüft", Assert.IsType<TextBlock>(prerequisites.Header).Text);
            prerequisites.IsExpanded = true;
            var texts = prerequisites.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains(texts, t => t?.Contains("Durchgängig bilingualer Bildungsgang: ungeprüft") == true);
            Assert.Contains(texts, t => t?.Contains("Religion/Ethik in Klasse 10") == true);
            Assert.DoesNotContain(prerequisites.GetLogicalDescendants().OfType<Control>(), c => c is ToggleButton or TextBox);
        }
        finally { window.Close(); }
    }
    private sealed class EmptyStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
