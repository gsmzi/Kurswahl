using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Application.Tests;

// Pflichtenheft §6 Nr.3/4; bestätigte F/L-Angebote und Korrekturauftrag 26.10.2026.
public class LanguageChoiceTests
{
    private static OfferCatalog Catalog => TestData.WorkflowCatalog with
    {
        Subjects = [.. TestData.WorkflowCatalog.Subjects, new("F", "Französisch"), new("L", "Latein")],
        Offers = [.. TestData.WorkflowCatalog.Offers, TestData.Offer("F-BF", "F"), TestData.Offer("L-BF", "L"),
            TestData.Offer("F-LF", "F", CourseType.Lf), TestData.Offer("L-LF", "L", CourseType.Lf)]
    };
    private sealed class MemoryStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new StoredChoice(choice, "synthetic-token"));
    }
    private static StudentSession Create(params string[] lfs)
    {
        var session = new StudentSession(Catalog, "demo-001", new MemoryStore());
        foreach (var subject in lfs) session.SetSelection(subject + "-LF", [1, 2, 3, 4]);
        session.NextStep();
        while (session.Choice.PlannedOralSubjects.Length < 2)
            session.SetOral(session.UsefulOralCandidates.First(c => c is not ("E" or "F" or "L" or "BIO" or "CH")), true);
        session.NextStep();
        return session;
    }
    [Fact] public void MissingLanguageIsAnExplicitChoiceAndNeverForcedToEnglish()
    {
        var session = Create("D", "M", "BIO");
        var task = session.Tasks.Single(t => t.Id == "PLAN-AREA-LANGUAGE");
        Assert.False(task.IsSatisfied);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "E");
        Assert.All(new[] { "E-BF", "F-BF", "L-BF" }, id => Assert.Contains(id, task.OptionOfferIds));
        session.ChooseTaskOption(task.Id, "F-BF", true);
        Assert.True(session.Tasks.Single(t => t.Id == task.Id).IsSatisfied);
        session.ChooseTaskOption(task.Id, "L-BF", true);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "F");
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "L");
    }
    [Theory] [InlineData("F-BF")] [InlineData("L-BF")] [InlineData("CH-BF")]
    public void SecondLanguageOrSecondScienceAreEquivalentOptionsWithoutSignedProfile(string offerId)
    {
        var session = Create("E", "BIO", "W");
        var task = session.Tasks.Single(t => t.Id == "PLAN-AREA-BREADTH");
        Assert.False(task.IsSatisfied);
        Assert.All(new[] { "F-BF", "L-BF", "CH-BF" }, id => Assert.Contains(id, task.OptionOfferIds));
        session.ChooseTaskOption(task.Id, offerId, true);
        Assert.True(session.Tasks.Single(t => t.Id == task.Id).IsSatisfied);
        Assert.DoesNotContain(session.Validation.Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
    }
    [Fact] public void SwitchingFromSecondScienceToLanguageReplacesOnlyThatAnswer()
    {
        var session = Create("E", "BIO", "W");
        session.ChooseTaskOption("PLAN-AREA-BREADTH", "CH-BF", true);
        session.ChooseTaskOption("PLAN-AREA-BREADTH", "F-BF", true);
        Assert.DoesNotContain(session.Choice.Selections, s => s.SubjectId == "CH");
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "BIO" && s.Type == CourseType.Lf);
        Assert.Contains(session.Choice.Selections, s => s.SubjectId == "F");
    }
    [Fact] public void TwoLanguagesDoNotRemoveFirstScienceRequirement()
    {
        var session = Create("D", "E", "F");
        Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-LANGUAGE").IsSatisfied);
        Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-BREADTH").IsSatisfied);
        Assert.False(session.Tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE").IsSatisfied);
        Assert.Contains(session.Validation.Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
    }
    [Fact] public void TwoSciencesDoNotRemoveForeignLanguageRequirement()
    {
        var session = Create("D", "BIO", "CH");
        Assert.False(session.Tasks.Single(t => t.Id == "PLAN-AREA-LANGUAGE").IsSatisfied);
        Assert.Contains(session.Validation.Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
    }
    [Fact] public void InformaticsInBreadthTaskStaysChosenBeforeFirstScienceIsSelected()
    {
        var inf = TestData.Offer("INF-BF", "INF") with { Requirements = [Requirement.Informatics] };
        var catalog = Catalog with { Subjects = Catalog.Subjects.Add(new("INF", "Informatik")), Offers = Catalog.Offers.Add(inf) };
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes });
        var session = new StudentSession(catalog, "demo-001", new MemoryStore(), profile);
        foreach (var s in new[] { "D", "M", "E" }) session.SetSelection(s + "-LF", [1, 2, 3, 4]);
        session.NextStep();
        session.SetOral("G", true);
        session.SetOral("ETH", true);
        session.NextStep();
        session.ChooseTaskOption("PLAN-AREA-BREADTH", "INF-BF", true);
        var breadth = session.Tasks.Single(t => t.Id == "PLAN-AREA-BREADTH");
        Assert.Contains("INF-BF", breadth.ChosenOfferIds);
        Assert.False(breadth.IsSatisfied); // first science still missing
        session.ChooseTaskOption("PLAN-AREA-SCIENCE", "BIO-BF", true);
        Assert.True(session.Tasks.Single(t => t.Id == "PLAN-AREA-BREADTH").IsSatisfied);
    }
}
