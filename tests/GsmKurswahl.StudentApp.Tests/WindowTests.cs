using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
    public async Task WindowCheckboxSaveAndReloadWorkWithActualJsonStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gsm-ui-" + Guid.NewGuid());
        var path = Path.Combine(directory, "aktuell.json");
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", new JsonChoiceStore(path));
        var window = new MainWindow(session, path, "demo-account");
        try
        {
            window.Show();
            CheckBox Box(string name) => window.GetVisualDescendants().OfType<CheckBox>().Single(b => b.Name == name);
            var next = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextStep");
            Assert.False(next.IsEnabled);
            foreach (var subject in new[] { "E", "BIO", "W" }) Box("Offer_" + subject + "_LF").IsChecked = true;
            Assert.True(next.IsEnabled);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(Box("Oral_D").IsChecked);
            Assert.False(Box("Oral_D").IsEnabled);
            Assert.True(Box("Oral_M").IsChecked);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(3, session.Step);
            Assert.False(Box("Offer_G_BF").IsEnabled);
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(4, session.Step);
            Box("Offer_BK_BF").IsChecked = true;
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
    public void RestrictedOfferIsDisabledAndReasonVisible()
    {
        var offer = TestData.Offer("D-LF", type: CourseType.Lf) with { Variant = CourseVariant.Bilingual, Requirements = [Requirement.ContinuousBilingual] };
        var window = new MainWindow(new StudentSession(TestData.Catalog with { Offers = [offer] }, "demo-001", new EmptyStore()), "demo.json", "demo-account");
        try
        {
            window.Show();
            Assert.False(window.GetVisualDescendants().OfType<CheckBox>().Single(b => b.Name == "Offer_D_LF").IsEnabled);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Persönliche Voraussetzung ungeprüft") == true);
        }
        finally { window.Close(); }
    }
    private sealed class EmptyStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
