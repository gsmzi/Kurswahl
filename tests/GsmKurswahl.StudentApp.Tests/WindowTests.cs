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
    public static AppBuilder BuildAvaloniaApp()
    {
        var render = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS"));
        var builder = AppBuilder.Configure<TestApp>();
        if (render) builder = builder.UseSkia();
        return builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !render });
    }
}
public sealed class TestApp : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public class WindowTests
{
    [AvaloniaFact]
    public async Task HoursWarningPersistsAcrossStepsAndReloadAndDisappearsAfterReduction()
    {
        var catalog = TestData.WorkflowCatalog;
        catalog = catalog with { Offers = [.. catalog.Offers.Select(o => o.Type == CourseType.Lf
            ? o with { Schedule = [.. o.Schedule.Select(h => h with { WeeklyHours = 5 })] } : o)] };
        var session = new StudentSession(catalog, "demo-001", new UiMemoryStore());
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            Border Warning() => window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "HoursWarning");
            Assert.False(Warning().IsVisible);
            foreach (var subject in new[] { "D", "M", "E" }) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
            session.NextStep(); session.SetOral("BIO", true); session.SetOral("G", true); session.NextStep();
            foreach (var subject in new[] { "BK", "ETH", "CH" }) session.SetSelection(subject + "-BF", [1, 2, 3, 4]);
            session.SetSelection("GK-BF", [1, 2, 3, 4]);
            Assert.True(Warning().IsVisible);
            Assert.Contains("HJ 3: 38 Wochenstunden", ((TextBlock)Warning().Child!).Text);
            session.NextStep(); Assert.True(Warning().IsVisible);
            session.NextStep(); Assert.True(Warning().IsVisible);
            window.UpdateLayout();
            var origin = Warning().TranslatePoint(default, window);
            Assert.NotNull(origin);
            Assert.True(origin.Value.Y >= 0 && origin.Value.Y + Warning().Bounds.Height <= window.ClientSize.Height);
            await session.SaveAsync(); await session.LoadAsync();
            Assert.True(Warning().IsVisible);
            session.SetSelection("CH-BF", []);
            Assert.False(Warning().IsVisible);
        }
        finally { await session.SaveAsync(); window.Close(); }
    }

    [AvaloniaFact]
    public void ContinuingReligionHasNoBlanketBadgeAndSwitchToEthicsShowsAction()
    {
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = TestData.WorkflowCatalog.Subjects.Add(new("KR", "Katholische Religion")),
            Offers = TestData.WorkflowCatalog.Offers.Add(TestData.Offer("KR-BF", "KR"))
        };
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with { ReligionEthicsGrade10 = "KR" });
        var session = new StudentSession(catalog, "demo-001", new UiMemoryStore(), profile);
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            foreach (var subject in new[] { "E", "BIO", "W" }) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
            session.NextStep(); session.NextStep();
            session.ChooseTaskOption("PLAN-AREA-RELIGION", "KR-BF", true);
            Assert.Null(session.Plan.Single(r => r.OfferId == "KR-BF").Issue);
            session.NextStep();
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ToggleButton>(), b => b.Name == "Offer_ETH_BF");
            session.GoToStep(3);
            session.ChooseTaskOption("PLAN-AREA-RELIGION", "ETH-BF", true);
            Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "KR");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Austritt aus Religionsunterricht muss beantragt werden.") == true);
            session.NextStep(); session.NextStep();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Für mündliche Prüfung in Ethik ist Feststellungsprüfung notwendig.") == true);
        }
        finally { session.SaveAsync().GetAwaiter().GetResult(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task ShippedCatalogShowsFrenchLatinAndScienceAsAlternatives()
    {
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        var session = new StudentSession(catalog, "demo-001", new UiMemoryStore());
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            foreach (var s in new[] { "E", "BIO", "W" }) session.SetSelection(s + "-LF", [1, 2, 3, 4]);
            session.NextStep(); session.NextStep();
            session.ChooseTaskOption("PLAN-AREA-ART", "BK-BF", true);
            session.ChooseTaskOption("PLAN-AREA-RELIGION", "ETH-BF", true);
            window.UpdateLayout();
            var toggles = window.GetVisualDescendants().OfType<ToggleButton>().ToArray();
            foreach (var id in new[] { "Option_F_BF", "Option_L_BF", "Option_CH_BF" })
                Assert.Contains(toggles, b => b.Name == id && b.IsEnabled);
            toggles.Single(b => b.Name == "Option_F_BF").IsChecked = true;
            Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-BREADTH").IsSatisfied);
            Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "CH");
        }
        finally { await session.SaveAsync(); window.Close(); }
    }
    [AvaloniaFact]
    public async Task ReviewShowsControlSheetAndRequiresExamPlanning()
    {
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", new UiMemoryStore());
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            foreach (var subject in new[] { "D", "M", "E" }) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
            session.NextStep();
            window.UpdateLayout();
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "DeferOralButton");
            Assert.False(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextStep").IsEnabled);
            session.SetOral("BIO", true);
            session.SetOral("G", true);
            session.NextStep(); session.NextStep(); session.NextStep();
            await session.SaveAsync();
            window.UpdateLayout();
            Assert.Equal(new[] { "BIO", "G" }, session.Choice.PlannedOralSubjects);
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "ControlSheetButton");
            Assert.Contains("PDF", window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ControlSheetButton").Content?.ToString());
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Content?.ToString()?.Contains("Drucken") == true);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), b => b.Text?.Contains("Mögliche mündliche Kombinationen") == true);
            var visibleTexts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToArray();
            Assert.DoesNotContain(visibleTexts, t => t.Contains("Ausbaustufen") || t.Contains("needs_review"));
            Assert.Contains(visibleTexts, t => t.Contains("Wer klärt offene Punkte? Die Oberstufenberatung"));
            Assert.Contains(visibleTexts, t => t.Contains("Deine Wahl wird automatisch geprüft."));
            var sportRow = session.Plan.Single(r => r.OfferId == "SP-BF");
            Assert.Equal(ReviewStatus.NeedsReview, sportRow.Issue);
            Assert.Contains("AGVO-SPORT", sportRow.IssueText);
            Assert.Contains(window.GetVisualDescendants().OfType<Control>(), c => ToolTip.GetTip(c)?.ToString()?.Contains("AGVO-SPORT") == true);
        }
        finally { if (session.IsDirty) await session.SaveAsync(); window.Close(); }
    }
    [AvaloniaTheory]
    [InlineData(1920, 1000)]
    [InlineData(1536, 780)]
    [InlineData(1280, 640)]
    public void FullHdWorkingAreasKeepHeaderAndNavigationAccessible(int width, int height)
    {
        // Logical client areas approximating Full HD at 100/125/150%, after window chrome/taskbar.
        // This checks layout constraints, not native DPI rendering.
        var window = new MainWindow(new StudentSession(TestData.WorkflowCatalog, "demo-001", new EmptyStore()), "demo.json", "demo-account")
        { WindowState = WindowState.Normal, Width = width, Height = height };
        try
        {
            window.Show();
            window.UpdateLayout();
            Control Find(string name) => window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
            Rect Area(Control control)
            {
                var origin = control.TranslatePoint(default, window);
                Assert.NotNull(origin);
                return new Rect(origin.Value, control.Bounds.Size);
            }
            var heading = Area(Find("WindowHeading"));
            var toolbar = Area(Find("FileToolbar"));
            Assert.False(heading.Intersects(toolbar));
            foreach (var name in new[] { "SaveButton", "LoadButton", "PreviousStep", "NextStep", "Step_5", "Prerequisites" })
            {
                var area = Area(Find(name));
                Assert.True(area.Width > 0 && area.Height > 0, name);
                Assert.True(area.Left >= 0 && area.Top >= 0 && area.Right <= window.ClientSize.Width + 1 && area.Bottom <= window.ClientSize.Height + 1, name);
            }
        }
        finally { window.Close(); }
    }

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
            Assert.False(Has("Sem_GK_BF_3"));
            Assert.False(Has("Sem_GK_BF_4"));
            Box("Sem_GK_BF_3_4").IsChecked = true;
            Assert.Equal(new[] { 1, 2, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Box("Sem_GK_BF_2").IsChecked = false;
            Assert.Equal(new[] { 1, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Box("Sem_GK_BF_3_4").IsChecked = false;
            Assert.Equal(new[] { 1 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Box("Sem_GEO_BF_4").IsChecked = true;
            Assert.Equal(new[] { 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GEO").Semesters);
            Box("Sem_GEO_BF_4").IsChecked = false;
            Box("Offer_BK_BF").IsChecked = true;
            Assert.Contains(session.Choice.Selections, s => s.SubjectId == "BK");
            Assert.True(Has("Offer_BK_BF"));
            Assert.False(Has("Sem_BK_BF_1"));
            Capture(window, "student-year-pairs");
            Box("Sem_BK_BF_1_2").IsChecked = false;
            Assert.Equal(new[] { 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "BK").Semesters);
            Box("Sem_BK_BF_1_2").IsChecked = true;
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
    public async Task CompletedChoicePromotesBasicToLfThroughVisibleControls()
    {
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", new UiMemoryStore());
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            ToggleButton Box(string name) => window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == name);
            Button Button(string name) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);
            void Click(string name) => Button(name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            foreach (var subject in new[] { "D", "M", "W" }) Box("Offer_" + subject + "_LF").IsChecked = true;
            Click("NextStep");
            Box("Oral_E").IsChecked = true; Box("Oral_BIO").IsChecked = true;
            Click("NextStep");
            Box("Option_BK_BF").IsChecked = true;
            Box("Option_ETH_BF").IsChecked = true;
            Box("Option_CH_BF").IsChecked = true;
            Click("NextStep"); Click("NextStep");
            Assert.Equal(5, session.Step);
            Click("Step_1");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ToggleButton>(), b => b.Name == "Offer_E_BF");
            Box("Offer_W_LF").IsChecked = false;
            Box("Offer_E_LF").IsChecked = true;
            Assert.True(Button("NextStep").IsEnabled);
            Assert.Equal("E-LF", Assert.Single(session.Choice.Selections.Where(s => s.SubjectId == "E")).OfferId);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Basisfachbelegung wurde automatisch entfernt") == true);
            Capture(window, "student-lf-change");
            Click("NextStep");
            Assert.False(Button("NextStep").IsEnabled);
            Box("Oral_G").IsChecked = true;
            Click("NextStep");
            Assert.Equal(new[] { 1, 2 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Assert.Equal(new[] { 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GEO").Semesters);
            Click("NextStep"); Click("NextStep");
            Assert.Equal(5, session.Step);
            Assert.DoesNotContain(session.Validation.Messages, m => m.Rule.Id is "SUBJECT-DUPLICATE" or "AGVO-SEMESTERS");
        }
        finally { await session.SaveAsync(); window.Close(); }
    }

    private static void Capture(MainWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("GSM_UI_SCREENSHOTS") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap); bitmap.Save(Path.Combine(directory, name + ".png"));
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
    private sealed class UiMemoryStore : IChoiceStore
    {
        private StoredChoice? value;
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
        {
            value = new(choice, "synthetic-token");
            return Task.FromResult(value);
        }
    }
    private sealed class EmptyStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
