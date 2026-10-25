using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Rules.Tests;

public class ChoiceValidatorTests
{
    private readonly ChoiceValidator validator = new();
    [Fact] public void EvenEmptyDraftCannotBecomeGreen() => Assert.Equal(ReviewStatus.NeedsReview,
        validator.Validate(TestData.Choice, TestData.Catalog, ProfileContext.Missing).Status);
    [Fact] public void UnverifiedYesDoesNotGrantPermission()
    {
        var offer = TestData.Offer() with { Requirements = [Requirement.Informatics] };
        var profile = new ProfileContext(ProfileStatus.Unverified, PriorEducation.Unknown with { InformaticsImp = Knowledge.Yes });
        Assert.False(validator.GetAvailability(offer, profile).CanSelect);
    }
    [Theory] [InlineData(Knowledge.No, Knowledge.No, false, ReviewStatus.Invalid)]
    [InlineData(Knowledge.Unknown, Knowledge.No, false, ReviewStatus.NeedsReview)]
    [InlineData(Knowledge.Yes, Knowledge.No, true, ReviewStatus.NeedsReview)]
    [InlineData(Knowledge.No, Knowledge.Yes, true, ReviewStatus.NeedsReview)]
    public void InformaticsUsesEitherVerifiedAccessRoute(Knowledge bridge, Knowledge imp, bool allowed, ReviewStatus status)
    {
        // Pflichtenheft §§4–5: getrennte Zugangswege; keine AGVO-Gesamtfreigabe.
        var result = validator.GetAvailability(TestData.Offer() with { Requirements = [Requirement.Informatics] },
            new(ProfileStatus.Verified, PriorEducation.Unknown with { InformaticsBridge = bridge, InformaticsImp = imp }));
        Assert.Equal(allowed, result.CanSelect); Assert.Equal(status, result.Status);
    }
    [Theory] [InlineData(Requirement.ItalianProfile)] [InlineData(Requirement.ContinuousBilingual)]
    public void UnknownPersonalRequirementsRemainReview(Requirement requirement) => Assert.Equal(ReviewStatus.NeedsReview,
        validator.GetAvailability(TestData.Offer() with { Requirements = [requirement] }, new(ProfileStatus.Verified, PriorEducation.Unknown)).Status);
    [Fact] public void PlannedOfferIsNotSelectable() => Assert.False(validator.GetAvailability(TestData.Offer() with { Status = OfferStatus.Planned }, ProfileContext.Missing).CanSelect);
    [Fact] public void WrongCatalogVersionIsInvalid() => Assert.Equal(ReviewStatus.Invalid,
        validator.Validate(TestData.Choice with { OfferVersion = "old" }, TestData.Catalog, ProfileContext.Missing).Status);
    [Fact] public void UnknownOfferIsInvalid() => Assert.Equal(ReviewStatus.Invalid,
        validator.Validate(TestData.Choice with { Selections = [TestData.Select(TestData.Offer("unknown"))] }, TestData.Catalog, ProfileContext.Missing).Status);
    [Fact] public void DuplicateSubjectAcrossVariantsIsInvalid()
    {
        var other = TestData.Offer("D-LF", type: CourseType.Lf);
        var result = validator.Validate(TestData.Choice with { Selections = [TestData.Select(TestData.Offer()), TestData.Select(other)] },
            TestData.Catalog with { Offers = [TestData.Offer(), other] }, ProfileContext.Missing);
        Assert.Contains(result.Messages, m => m.Rule.Id == "SUBJECT-DUPLICATE");
    }
    [Fact] public void SiaHasTwoSemesterCoursesNotFour()
    {
        // Pflichtenheft §4: Belegungszahl; keine Aussage zur Anrechnung oder Sportersatz.
        var sia = TestData.Offer("SIA", "SIA", CourseType.Seminar) with { Schedule = [new(1, 3), new(2, 3)] };
        var catalog = TestData.Catalog with { Subjects = [new("SIA", "SIA")], Offers = [sia] };
        ModelGuard.Catalog(catalog);
        var summary = ChoiceValidator.Summarize(TestData.Choice with { Selections = [TestData.Select(sia)] }, catalog);
        Assert.Equal(2, summary.Sum(s => s.CourseCount)); Assert.Equal(0, summary[2].WeeklyHours);
    }
    [Fact] public void EveryRuleHasStableMetadata()
    {
        Assert.Equal(ChoiceValidator.Definitions.Length, ChoiceValidator.Definitions.Select(d => d.Id).Distinct().Count());
        Assert.All(ChoiceValidator.Definitions, d => { Assert.NotEmpty(d.Source); Assert.NotEmpty(d.Scope); Assert.Equal(ChoiceValidator.Version, d.Version); });
    }
}
