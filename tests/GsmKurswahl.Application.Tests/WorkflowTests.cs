using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

public class WorkflowTests
{
    private static StudentSession Create(params string[] subjects)
    {
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", new MemoryStore());
        foreach (var subject in subjects) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
        return session;
    }

    [Theory]
    [InlineData("D", "M", "E", true)]
    [InlineData("E", "BIO", "W", true)]
    [InlineData("E", "BIO", "CH", false)]
    [InlineData("D", "W", "W", false)]
    public void LfStepChecksCountCoreAndPossibleExamCombination(string a, string b, string c, bool canContinue)
    {
        // Pflichtenheft §6 Nr.1,11: necessary planning conditions, never admission.
        var session = Create(a, b, c);
        Assert.Equal(canContinue, session.StepError is null);
        if (!canContinue) Assert.Throws<InvalidOperationException>(session.NextStep);
    }

    [Fact]
    public async Task ForcedGermanMathAndEconomicsRequiredSemestersSurviveSaveLoad()
    {
        // Pflichtenheft §4, §6 Nr.8,11.
        var store = new MemoryStore();
        var session = new StudentSession(TestData.WorkflowCatalog, "demo-001", store);
        foreach (var s in new[] { "E", "BIO", "W" }) session.SetSelection(s + "-LF", [1, 2, 3, 4]);
        session.NextStep();
        Assert.Equal(new[] { "D", "M" }, session.Choice.PlannedOralSubjects);
        Assert.Throws<InvalidOperationException>(() => session.SetOral("D", false));
        session.NextStep();
        Assert.Equal(3, session.Step);
        Assert.Equal(new[] { 1 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
        Assert.Equal(new[] { 3 }, session.Choice.Selections.Single(s => s.SubjectId == "GEO").Semesters);
        Assert.DoesNotContain(session.Choice.Selections, s => s.OfferId == "E-BF");
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId is "BK" or "MU" or "ETH");
        await session.SaveAsync();
        var loaded = new StudentSession(TestData.WorkflowCatalog, "demo-001", store);
        await loaded.LoadAsync();
        Assert.Equal(new[] { "D", "M" }, loaded.Choice.PlannedOralSubjects);
        Assert.Equal(1, loaded.Step);
        Assert.Equal(ReviewStatus.NeedsReview, loaded.Validation.Status);
    }

    [Fact]
    public void OralCombinationMustCoverFieldsAndGkAddsFourSemesters()
    {
        var session = Create("D", "M", "E");
        session.NextStep();
        Assert.DoesNotContain("GEO", session.OralCandidates);
        Assert.DoesNotContain("D", session.OralCandidates);
        session.SetOral("BIO", true); session.SetOral("CH", true);
        Assert.NotNull(session.StepError);
        session.SetOral("CH", false); session.SetOral("GK", true);
        Assert.Null(session.StepError);
        session.NextStep();
        Assert.Equal(new[] { 1, 2, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
        Assert.Equal(new[] { 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GEO").Semesters);
    }

    [Fact]
    public void BacktrackingPreservesExtraCoursesAndRecomputesRequirements()
    {
        var session = Create("E", "BIO", "W");
        session.NextStep(); session.NextStep(); session.NextStep();
        session.SetSelection("GK-BF", [1, 2, 3, 4]);
        session.PreviousStep(); session.PreviousStep(); session.PreviousStep();
        session.SetSelection("W-LF", []); session.SetSelection("CH-LF", [1, 2, 3, 4]);
        Assert.NotNull(session.StepError);
        session.SetSelection("CH-LF", []); session.SetSelection("W-LF", [1, 2, 3, 4]);
        session.NextStep(); session.NextStep();
        Assert.Equal(new[] { 1, 2, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "GK").Semesters);
        Assert.Equal(1, session.Choice.Selections.Count(s => s.SubjectId == "GK"));
    }

    [Fact]
    public void MissingRequiredOfferIsReportedAndNeverFabricated()
    {
        var catalog = TestData.WorkflowCatalog;
        var session = new StudentSession(catalog with { Offers = [.. catalog.Offers.Where(o => o.SubjectId != "SP")] }, "demo-001", new MemoryStore());
        foreach (var s in new[] { "E", "BIO", "W" }) session.SetSelection(s + "-LF", [1, 2, 3, 4]);
        session.NextStep(); session.NextStep();
        Assert.Contains("(SP): needs_review", session.AutomaticMessage);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "SP");
    }

    [Fact]
    public void AreaTasksAskOnlyOpenDecisionsAndSingleChoiceReplacesPreviousAnswer()
    {
        // Pflichtenheft §6 Nr.3,4: PLAN-AREA-* planning questions, never admission.
        var session = Create("E", "BIO", "W");
        session.NextStep(); session.NextStep();
        var tasks = session.Tasks.ToDictionary(t => t.Id);
        Assert.All(tasks.Values, t => Assert.False(t.IsSatisfied));
        Assert.Equal(1, tasks["PLAN-AREA-SCIENCE"].Choose);
        Assert.Equal(new[] { "BIO" }, tasks["PLAN-AREA-SCIENCE"].FulfilledBySubjects);
        Assert.DoesNotContain("BIO-BF", tasks["PLAN-AREA-SCIENCE"].OptionOfferIds);
        Assert.Equal(new[] { "BK-BF", "MU-BF" }, tasks["PLAN-AREA-ART"].OptionOfferIds);
        session.ChooseTaskOption("PLAN-AREA-ART", "BK-BF", true);
        Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-ART").IsSatisfied);
        session.ChooseTaskOption("PLAN-AREA-ART", "MU-BF", true);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "BK");
        Assert.Equal(new[] { 1, 2, 3, 4 }, session.Choice.Selections.Single(s => s.SubjectId == "MU").Semesters);
        Assert.Throws<InvalidOperationException>(() => session.ChooseTaskOption("PLAN-AREA-ART", "CH-BF", true));
        Assert.Contains(session.PlanningNotes, n => n.StartsWith("PLAN-AREA-RELIGION"));
        Assert.DoesNotContain(session.PlanningNotes, n => n.StartsWith("PLAN-AREA-ART"));
    }

    [Fact]
    public void ScienceTaskWithoutScienceLfAllowsTwoAndKeepsRequiredOralScience()
    {
        var session = Create("D", "M", "E");
        session.NextStep();
        session.SetOral("BIO", true); session.SetOral("GK", true);
        session.NextStep();
        var science = session.Tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE");
        Assert.Equal(2, science.Choose);
        Assert.Equal(1, science.Open);
        Assert.Contains("BIO-BF", science.ChosenOfferIds);
        Assert.Throws<InvalidOperationException>(() => session.ChooseTaskOption("PLAN-AREA-SCIENCE", "BIO-BF", false));
        session.ChooseTaskOption("PLAN-AREA-SCIENCE", "CH-BF", true);
        Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE").IsSatisfied);
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "BIO");
        Assert.Equal(new[] { 1, 2, 3, 4 }, session.LockedSemesters("BIO-BF"));
    }

    [Fact]
    public void SingleScienceChoiceNeverRemovesARequiredCourse()
    {
        var catalog = TestData.WorkflowCatalog;
        var session = new StudentSession(catalog with { Offers = catalog.Offers.Add(TestData.Offer("PH-BF", "PH")), Subjects = catalog.Subjects.Add(new("PH", "PH")) }, "demo-001", new MemoryStore());
        foreach (var subject in new[] { "D", "M", "CH" }) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
        session.NextStep();
        session.SetOral("BIO", true); session.SetOral("G", true);
        session.NextStep();
        var science = session.Tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE");
        Assert.True(science.IsExclusive);
        Assert.True(science.IsSatisfied);
        session.ChooseTaskOption("PLAN-AREA-SCIENCE", "PH-BF", true);
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "BIO");
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "PH");
    }

    [Fact]
    public void RestrictedInformaticsIsListedWithReasonButNeverOffered()
    {
        // Pflichtenheft §6 Nr.4,5: INF only with verified profile and only as second science.
        var catalog = TestData.WorkflowCatalog;
        var inf = TestData.Offer("INF-BF", "INF") with { Requirements = [Requirement.Informatics] };
        var session = new StudentSession(catalog with { Offers = catalog.Offers.Add(inf), Subjects = catalog.Subjects.Add(new("INF", "Informatik")) }, "demo-001", new MemoryStore());
        var science = session.Tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE");
        Assert.DoesNotContain("INF-BF", science.OptionOfferIds);
        Assert.Contains(science.Restricted, r => r.OfferId == "INF-BF" && r.Explanation.Contains("ungeprüft"));
        Assert.Contains(session.RestrictedOffers(lf: false), o => o.Id == "INF-BF");
        Assert.DoesNotContain(session.ExtraOffers, o => o.Id == "INF-BF");
    }

    [Fact]
    public void UsefulOralCandidatesHideSubjectsThatCannotCompleteACombination()
    {
        var session = Create("D", "M", "E");
        session.NextStep();
        Assert.Contains("BIO", session.UsefulOralCandidates);
        session.SetOral("BIO", true);
        Assert.Contains("G", session.UsefulOralCandidates);
        Assert.DoesNotContain("CH", session.UsefulOralCandidates);
        session.SetOral("G", true);
        Assert.Empty(session.UsefulOralCandidates);
    }

    [Fact]
    public void PlanShowsAutomaticOralAndOpenRowsAndCountsTowardsMinimum()
    {
        var session = Create("E", "BIO", "W");
        session.NextStep(); session.NextStep();
        var plan = session.Plan;
        Assert.Equal(new[] { "E", "BIO", "W" }, plan.Take(3).Select(r => r.OfferId![..r.OfferId!.IndexOf('-')]));
        Assert.True(plan.Single(r => r.OfferId == "D-BF").IsAutomatic);
        Assert.True(plan.Single(r => r.OfferId == "D-BF").IsOral);
        Assert.Equal(new[] { 1 }, plan.Single(r => r.OfferId == "GK-BF").Semesters);
        Assert.Equal(3, plan.Count(r => r.IsOpen));
        Assert.Equal(session.Choice.Selections.Sum(s => s.Semesters.Length), session.CountedCourses);
        Assert.Contains(session.PlanningNotes, n => n.StartsWith("PLAN-MIN-COURSES"));
        Assert.DoesNotContain(session.ExtraOffers, o => o.SubjectId is "D" or "BIO");
        session.NextStep(); session.NextStep();
        Assert.Equal(StudentSession.LastStep, session.Step);
        session.GoToStep(1);
        Assert.Equal(1, session.Step);
        session.GoToStep(4);
        Assert.Equal(1, session.Step);
    }

    private sealed class MemoryStore : IChoiceStore
    {
        private StoredChoice? stored;
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(stored);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
        {
            stored = new(choice, "synthetic-token"); return Task.FromResult(stored);
        }
    }
}
