using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;

namespace GsmKurswahl.StudentApp.Tests;

// Regressions for findings of the randomized click tests (29.10.2026): completed choice, then back and forward again.
public class StudentRevisionRegressionTests
{
    private static OfferCatalog Shipped => JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));

    private sealed class Harness : IDisposable
    {
        public StudentSession Session { get; }
        public MainWindow Window { get; }
        public Harness(ProfileContext? profile = null)
        {
            Session = new StudentSession(Shipped, "demo-001", new NullStore(), profile);
            Window = new MainWindow(Session, "demo.json", "demo-account");
            Window.Show();
        }
        public ToggleButton Box(string name) { Window.UpdateLayout(); return Window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == name); }
        public void Click(string name) { Window.UpdateLayout(); Window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name && b is not ToggleButton).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        public bool Has(string offerId) => Session.Choice.Selections.Any(s => s.OfferId == offerId);
        public bool Shows(string text) { Window.UpdateLayout(); return Window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(text) == true); }
        // LF D, M, BIO; oral E + G; art BK, religion ETH; then step 4 with F-BF and CH-BF added.
        public void CompleteWithTwoBreadthCourses()
        {
            foreach (var s in new[] { "D", "M", "BIO" }) Box($"Offer_{s}_LF").IsChecked = true;
            Click("NextStep");
            Box("Oral_E").IsChecked = true; Box("Oral_G").IsChecked = true;
            Click("NextStep");
            Box("Option_BK_BF").IsChecked = true; Box("Option_ETH_BF").IsChecked = true;
            Click("NextStep");
            Box("Offer_F_BF").IsChecked = true; Box("Offer_CH_BF").IsChecked = true;
            Click("NextStep");
            Assert.Equal(5, Session.Step);
        }
        // Saved first: closing with unsaved changes would leave the confirmation dialog open.
        public void Dispose() { Session.SaveAsync().GetAwaiter().GetResult(); Window.Close(); }
    }

    [AvaloniaFact]
    public void TaskCardShowsEverySelectedCourseAndClickingNeverDeletesAnotherCourse()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_3");
        h.Click("Task_PLAN_AREA_BREADTH");
        // Both matching courses are visible as chosen; check boxes instead of a radio group.
        Assert.True(h.Box("Option_F_BF").IsChecked);
        Assert.True(h.Box("Option_CH_BF").IsChecked);
        Assert.IsType<CheckBox>(h.Box("Option_CH_BF"));
        Assert.True(h.Shows("mehrere passende Kurse"));
        // Re-selecting an already selected course through the session is a no-op.
        var before = h.Session.Choice;
        h.Session.ChooseTaskOption("PLAN-AREA-BREADTH", "CH-BF", true);
        Assert.Equal(before, h.Session.Choice);
        // Adding a third course keeps the others; removing is explicit.
        h.Box("Option_L_BF").IsChecked = true;
        Assert.True(h.Has("F-BF") && h.Has("CH-BF") && h.Has("L-BF"));
        h.Box("Option_F_BF").IsChecked = false;
        Assert.False(h.Has("F-BF"));
        Assert.True(h.Has("CH-BF") && h.Has("L-BF"));
    }

    [AvaloniaFact]
    public void SingleAnswerIsStillReplacedByRadioButton()
    {
        using var h = new Harness();
        foreach (var s in new[] { "D", "M", "BIO" }) h.Box($"Offer_{s}_LF").IsChecked = true;
        h.Click("NextStep");
        h.Box("Oral_E").IsChecked = true; h.Box("Oral_G").IsChecked = true;
        h.Click("NextStep");
        h.Box("Option_BK_BF").IsChecked = true; h.Box("Option_ETH_BF").IsChecked = true;
        h.Box("Option_F_BF").IsChecked = true;
        h.Click("Task_PLAN_AREA_BREADTH");
        Assert.IsType<RadioButton>(h.Box("Option_CH_BF"));
        h.Box("Option_CH_BF").IsChecked = true;
        Assert.False(h.Has("F-BF"));
        Assert.True(h.Has("CH-BF"));
    }

    [AvaloniaFact]
    public void GivingUpPromotedLfRestoresBasicCourseAndClearsStaleNotice()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_1");
        h.Box("Offer_BIO_LF").IsChecked = false;
        h.Box("Offer_F_LF").IsChecked = true;
        Assert.False(h.Has("F-BF"));
        Assert.True(h.Shows("Französisch ist jetzt Leistungsfach"));
        h.Box("Offer_F_LF").IsChecked = false;
        Assert.True(h.Has("F-BF"));
        Assert.Equal(new[] { 1, 2, 3, 4 }, h.Session.Choice.Selections.Single(s => s.OfferId == "F-BF").Semesters);
        Assert.False(h.Shows("Französisch ist jetzt Leistungsfach"));
        Assert.True(h.Shows("Französisch ist kein Leistungsfach mehr"));
        h.Box("Offer_BIO_LF").IsChecked = true;
        for (var i = 0; i < 4; i++) h.Click("NextStep");
        Assert.Equal(5, h.Session.Step);
        Assert.True(h.Has("F-BF") && h.Has("CH-BF"));
    }

    [AvaloniaFact]
    public void GivingUpPromotedOralLfRestoresOralPlanning()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_1");
        h.Box("Offer_BIO_LF").IsChecked = false;
        h.Box("Offer_E_LF").IsChecked = true;
        Assert.DoesNotContain("E", h.Session.Choice.PlannedOralSubjects);
        h.Box("Offer_E_LF").IsChecked = false;
        h.Box("Offer_BIO_LF").IsChecked = true;
        Assert.True(h.Has("E-BF"));
        Assert.Equal(new[] { "G", "E" }, h.Session.Choice.PlannedOralSubjects);
        h.Click("NextStep");
        Assert.Null(h.Session.StepError);
    }

    [AvaloniaFact]
    public void FourthLfIsDisabledAndRejectedWithoutRemovingBasicCourse()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_1");
        Assert.False(h.Box("Offer_F_LF").IsEnabled);
        Assert.True(h.Box("Offer_D_LF").IsEnabled);
        var before = h.Session.Choice;
        var ex = Assert.Throws<InvalidOperationException>(() => h.Session.SetSelection("F-LF", [1, 2, 3, 4]));
        Assert.Contains("bereits drei Leistungsfächer", ex.Message);
        Assert.Equal(before, h.Session.Choice);
    }

    [AvaloniaFact]
    public void SportExemptionOffersOnlyCompleteSchoolYearsInStepFour()
    {
        // Nutzerfestlegung 29.10.2026: no single-semester sport replacement at course selection.
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with { SportExemptSemesters = [2], PermanentSportExemption = Knowledge.No });
        var session = new StudentSession(Shipped, "demo-001", new NullStore(), profile);
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            ToggleButton Box(string name) { window.UpdateLayout(); return window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == name); }
            bool Has(string name) { window.UpdateLayout(); return window.GetVisualDescendants().OfType<Control>().Any(b => b.Name == name); }
            void Next() { window.UpdateLayout(); window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextStep").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            foreach (var s in new[] { "D", "M", "BIO" }) Box($"Offer_{s}_LF").IsChecked = true;
            Next();
            Box("Oral_E").IsChecked = true; Box("Oral_G").IsChecked = true;
            Next(); Next();
            Assert.Equal(new[] { 1, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "SP").Semesters);
            // Before 29.10.2026 single half-years HJ 3 / HJ 4 were offered here because of the sport exemption.
            Assert.True(Has("Sem_GK_BF_3_4"));
            Assert.False(Has("Sem_GK_BF_3") || Has("Sem_GK_BF_4"));
            Box("Sem_GK_BF_3_4").IsChecked = true;
            Assert.Equal(new[] { 1, 2, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Assert.DoesNotContain(session.Validation.Messages, m => m.Rule.Id == "AGVO-SEMESTERS");
        }
        finally { session.SaveAsync().GetAwaiter().GetResult(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task LoadedDraftWithUnknownOfferDoesNotBreakTasksOrWindow()
    {
        var store = new FixedStore();
        var session = new StudentSession(Shipped, "demo-001", store);
        store.Value = new(session.Choice with
        {
            Selections = [new("D-LF", "D", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]), new("M-LF", "M", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("BIO-LF", "BIO", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]), new("E-BF-ALT", "E", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4])]
        }, "t");
        var window = new MainWindow(session, "demo.json", "demo-account");
        try
        {
            window.Show();
            await session.LoadAsync();
            // Unknown offers stay in the draft and are reported, but never answer a task.
            Assert.False(session.Tasks.Single(t => t.Id == "PLAN-AREA-LANGUAGE").IsSatisfied);
            Assert.Contains(session.Validation.Messages, m => m.Rule.Id == "OFFER-SELECTION");
            // Step 3 renders the task cards for this draft without an exception.
            session.NextStep(); session.SetOral("E", true); session.SetOral("G", true); session.NextStep();
            Assert.Equal(3, session.Step);
            window.UpdateLayout();
            Assert.Contains("(E)", session.AutomaticMessage);
        }
        finally { await session.SaveAsync(); window.Close(); }
        Assert.Contains(session.Choice.Selections, s => s.OfferId == "E-BF-ALT");
    }

    [AvaloniaFact]
    public void CompletedChoiceSurvivesRepeatedBackButtonsAndOralChanges()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        var original = h.Session.Choice.Selections.ToDictionary(s => s.OfferId, s => s.Semesters.ToArray());
        for (var cycle = 0; cycle < 8; cycle++)
        {
            // Use the actual Back button all the way, not just the stepper shortcut.
            for (var step = 5; step > 1; step--) h.Click("PreviousStep");
            h.Click("NextStep");
            h.Box("Oral_G").IsChecked = false;
            h.Box("Oral_GK").IsChecked = true;
            for (var step = 2; step < 5; step++) h.Click("NextStep");
            Assert.Equal(new[] { 1, 2, 3, 4 }, h.Session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            h.Click("Step_2");
            h.Box("Oral_GK").IsChecked = false;
            h.Box("Oral_G").IsChecked = true;
            for (var step = 2; step < 5; step++) h.Click("NextStep");
            Assert.Equal(5, h.Session.Step);
            foreach (var (id, semesters) in original)
                Assert.All(semesters, semester => Assert.Contains(semester, h.Session.Choice.Selections.Single(s => s.OfferId == id).Semesters));
            Assert.Contains(h.Session.ExamOptions, e => (e.First == "E" && e.Second == "G") || (e.First == "G" && e.Second == "E"));
        }
    }

    [AvaloniaFact]
    public void EconomicsLfRoundTripsRecalculateSemesterLocksWithoutLosingExtras()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        for (var cycle = 0; cycle < 4; cycle++)
        {
            h.Click("Step_1");
            h.Box("Offer_BIO_LF").IsChecked = false;
            h.Box("Offer_W_LF").IsChecked = true;
            h.Click("NextStep");
            h.Box("Oral_G").IsChecked = false;
            h.Box("Oral_GK").IsChecked = true;
            h.Click("NextStep"); h.Click("NextStep");
            Assert.Equal(new[] { 1, 2, 3, 4 }, h.Session.LockedSemesters("GK-BF"));
            Assert.Equal(new[] { 3 }, h.Session.LockedSemesters("GEO-BF"));
            Assert.Throws<InvalidOperationException>(() => h.Session.SetSelection("GK-BF", [1, 3, 4]));
            h.Box("Sem_GEO_BF_4").IsChecked = false;
            h.Click("NextStep");
            h.Click("Step_1");
            h.Box("Offer_W_LF").IsChecked = false;
            h.Box("Offer_BIO_LF").IsChecked = true;
            for (var step = 1; step < 5; step++) h.Click("NextStep");
            Assert.Equal(5, h.Session.Step);
            Assert.Equal(new[] { 1, 2, 3, 4 }, h.Session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
            Assert.Equal(new[] { 3, 4 }, h.Session.Choice.Selections.Single(s => s.SubjectId == "GEO").Semesters);
            Assert.True(h.Has("F-BF") && h.Has("CH-BF"));
            h.Click("Step_2");
            h.Box("Oral_GK").IsChecked = false;
            h.Box("Oral_G").IsChecked = true;
            for (var step = 2; step < 5; step++) h.Click("NextStep");
        }
    }

    [AvaloniaFact]
    public void RemovingGermanLfForcesOralGermanAndReturningReleasesItAgain()
    {
        using var h = new Harness();
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_1");
        h.Box("Offer_D_LF").IsChecked = false;
        h.Box("Offer_E_LF").IsChecked = true;
        h.Click("NextStep");
        Assert.Contains("D", h.Session.ForcedOral);
        Assert.Equal(new[] { "D", "G" }, h.Session.Choice.PlannedOralSubjects);
        for (var step = 2; step < 5; step++) h.Click("NextStep");
        Assert.True(h.Has("D-BF"));
        h.Click("Step_1");
        h.Box("Offer_E_LF").IsChecked = false;
        h.Box("Offer_D_LF").IsChecked = true;
        h.Click("NextStep");
        Assert.Empty(h.Session.ForcedOral);
        // E was displaced while oral slots were full (D/G). It can now be chosen again.
        if (!h.Session.Choice.PlannedOralSubjects.Contains("E")) h.Box("Oral_E").IsChecked = true;
        for (var step = 2; step < 5; step++) h.Click("NextStep");
        Assert.Equal(5, h.Session.Step);
        Assert.True(h.Has("E-BF") && h.Has("F-BF") && h.Has("CH-BF"));
        Assert.False(h.Has("D-BF"));
        Assert.DoesNotContain("D", h.Session.Choice.PlannedOralSubjects);
    }

    [AvaloniaFact]
    public void OralBiologyWithBilingualPermissionRequiresExplicitVariantAfterBacktracking()
    {
        using var h = new Harness(new ProfileContext(ProfileStatus.Verified,
            PriorEducation.Unknown with { ContinuousBilingual = Knowledge.Yes }));
        h.CompleteWithTwoBreadthCourses();
        h.Click("Step_1");
        h.Box("Offer_BIO_LF").IsChecked = false;
        h.Box("Offer_GK_LF").IsChecked = true;
        h.Click("NextStep");
        h.Box("Oral_G").IsChecked = false;
        h.Box("Oral_BIO").IsChecked = true;
        h.Click("NextStep");
        // CH already satisfies the science area, but it cannot replace oral BIO.
        Assert.True(h.Has("CH-BF"));
        var task = Assert.Single(h.Session.Tasks.Where(t => t.Id == "PLAN-VARIANT-BIO"));
        Assert.False(task.IsSatisfied);
        Assert.Equal(2, task.OptionOfferIds.Length);
        h.Box("Option_BIO_BF").IsChecked = true;
        h.Click("NextStep"); h.Click("NextStep");
        Assert.True(h.Has("BIO-BF"));
        Assert.DoesNotContain(h.Session.Tasks, t => t.Id == "PLAN-VARIANT-BIO");
        Assert.Contains(h.Session.ExamOptions, e => (e.First == "E" && e.Second == "BIO") || (e.First == "BIO" && e.Second == "E"));
    }

    private sealed class NullStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => Task.FromResult(new StoredChoice(choice, "t"));
    }
    private sealed class FixedStore : IChoiceStore
    {
        public StoredChoice? Value;
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) => Task.FromResult(Value = new StoredChoice(choice, "t2"));
    }
}
