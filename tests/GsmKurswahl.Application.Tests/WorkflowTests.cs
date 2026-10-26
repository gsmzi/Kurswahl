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
        Assert.Contains("SP: needs_review", session.AutomaticMessage);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "SP");
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
