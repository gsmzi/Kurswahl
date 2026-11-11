using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Rules.Tests;

// GSM-Nutzerfestlegung 11.11.2026: bestätigtes Klasse-10-Fach bestimmt die Religion, Ethik bleibt frei.
public class ReligionAvailabilityTests
{
    [Theory]
    [InlineData("ER", "ER", true)]
    [InlineData("ER", "KR", false)]
    [InlineData("KR", "ER", false)]
    [InlineData("KR", "KR", true)]
    [InlineData("ETH", "ER", false)]
    [InlineData("ETH", "KR", false)]
    [InlineData("ER", "ETH", true)]
    [InlineData("KR", "ETH", true)]
    [InlineData("ETH", "ETH", true)]
    [InlineData(null, "ETH", true)]
    public void ConfirmedGradeTenSubjectDeterminesAvailability(string? grade10, string subject, bool allowed)
    {
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with
        { ReligionEthicsGrade10 = grade10, ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.Yes });
        var offer = TestData.Offer(subject + "-BF", subject);
        var result = new ChoiceValidator().GetAvailability(offer, profile);
        Assert.Equal(allowed, result.CanSelect);
        if (!allowed) Assert.Equal(ReviewStatus.Invalid, result.Status);
    }

    [Theory]
    [InlineData(ProfileStatus.Verified, null)]
    [InlineData(ProfileStatus.Missing, "KR")]
    [InlineData(ProfileStatus.Unverified, "KR")]
    [InlineData(ProfileStatus.ProfileInvalid, "KR")]
    public void UnknownOrUntrustedSubjectAllowsReligionWithReview(ProfileStatus status, string? grade10)
    {
        var profile = new ProfileContext(status, PriorEducation.Unknown with { ReligionEthicsGrade10 = grade10, ReligionEthicsPermission = Knowledge.Yes });
        foreach (var subject in new[] { "ER", "KR" })
        {
            var offer = TestData.Offer(subject + "-BF", subject);
            var result = new ChoiceValidator().GetAvailability(offer, profile);
            Assert.True(result.CanSelect);
            Assert.Equal(ReviewStatus.NeedsReview, result.Status);
            var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new(subject, subject)), Offers = TestData.WorkflowCatalog.Offers.Add(offer) };
            var choice = TestData.Choice with { Selections = [TestData.Select(offer)] };
            var message = Assert.Single(new ChoiceValidator().Validate(choice, catalog, profile).Messages
                .Where(m => m.Rule.Id == "PREREQUISITE-UNKNOWN"));
            Assert.Equal(ReviewStatus.NeedsReview, message.Rule.Severity);
            Assert.Contains(subject, ChoiceWorkflow.OralCandidates(choice, catalog, profile));
            var task = ChoiceWorkflow.Tasks(choice, catalog, profile).Single(t => t.Id == "PLAN-AREA-RELIGION");
            Assert.Contains(offer.Id, task.OptionOfferIds);
            Assert.True(task.IsSatisfied);
        }
        Assert.True(new ChoiceValidator().GetAvailability(TestData.Offer("ETH-BF", "ETH"), profile).CanSelect);
    }

    [Theory]
    [InlineData("ER", "KR")]
    [InlineData("KR", "ER")]
    [InlineData("ETH", "KR")]
    public void StoredMismatchingReligionIsReportedAndNeverProposed(string grade10, string subject)
    {
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with
        { ReligionEthicsGrade10 = grade10, ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.Yes });
        var offer = TestData.Offer(subject + "-BF", subject);
        var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new(subject, subject)), Offers = TestData.WorkflowCatalog.Offers.Add(offer) };
        var choice = TestData.Choice with { Selections = [TestData.Select(offer)] };
        var before = choice.Selections;
        var result = new ChoiceValidator().Validate(choice, catalog, profile);
        var message = Assert.Single(result.Messages.Where(m => m.Rule.Id == "GSM-RELIGION-CONFESSION"));
        Assert.Equal(ReviewStatus.Invalid, message.Rule.Severity);
        Assert.Equal(new[] { subject }, message.SubjectIds);
        Assert.Equal(new[] { 1, 2, 3, 4 }, message.Semesters);
        Assert.DoesNotContain(subject, ChoiceWorkflow.OralCandidates(choice, catalog, profile));
        var task = ChoiceWorkflow.Tasks(choice, catalog, profile).Single(t => t.Id == "PLAN-AREA-RELIGION");
        Assert.DoesNotContain(offer.Id, task.OptionOfferIds);
        Assert.Contains(task.Restricted, r => r.OfferId == offer.Id);
        Assert.False(task.IsSatisfied);
        Assert.Equal(before, choice.Selections);
    }
}
