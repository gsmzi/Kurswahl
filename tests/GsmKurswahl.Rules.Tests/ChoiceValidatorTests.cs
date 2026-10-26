using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Rules.Tests;

public class ChoiceValidatorTests
{
    private readonly ChoiceValidator validator = new();
    [Fact] public void EvenEmptyDraftCannotBecomeGreen() => Assert.Equal(ReviewStatus.Invalid,
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
        Assert.All(CatalogRules.Definitions, d => Assert.Contains(d, ChoiceValidator.Definitions));
    }
    [Fact] public void EveryRuleIsDocumented()
    {
        var documentation = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "RECHTSQUELLEN.md"));
        Assert.All(ChoiceValidator.Definitions, d => Assert.Contains($"`{d.Id}`", documentation));
    }

    [Theory] [InlineData("INF")] [InlineData("I")] [InlineData("ETH")] [InlineData("ER")] [InlineData("KR")]
    public void GsmExcludedLfIsBlockedWithRuleId(string subject)
    {
        // Pflichtenheft §4 „Nicht als LF anbieten“; kein AGVO-Volltest.
        var offer = TestData.Offer(subject + "-LF", subject, CourseType.Lf);
        var availability = validator.GetAvailability(offer, ProfileContext.Missing);
        Assert.False(availability.CanSelect); Assert.Equal(ReviewStatus.Invalid, availability.Status);
        Assert.StartsWith("CATALOG-", availability.Explanation);
        Assert.Contains(CatalogRules.Check(offer), m => m.Rule.Id == "CATALOG-LF-EXCLUDED");
    }
    [Theory]
    [InlineData("lf-short", "CATALOG-LF-SEMESTERS")]
    [InlineData("psy-bf", "CATALOG-PSY")] [InlineData("psy-hj3", "CATALOG-PSY")] [InlineData("psy-hj1", "CATALOG-PSY")]
    [InlineData("sia-2h", "CATALOG-SIA")] [InlineData("geo-hj1", "CATALOG-GEO-BF")] [InlineData("geo-wf", "CATALOG-GEO-BF")]
    [InlineData("inf-no-req", "CATALOG-INF")] [InlineData("inf-2h", "CATALOG-INF")]
    [InlineData("i-no-req", "CATALOG-I-PROFILE")] [InlineData("i-wf", "CATALOG-I-PROFILE")] [InlineData("bili-no-req", "CATALOG-BILINGUAL")]
    public void CatalogRuleViolationIsReportedAndBlocksOffer(string kind, string ruleId)
    {
        // Pflichtenheft §4/§6: Angebotsgrenzen; Quelle je Regel in CatalogRules.Definitions.
        var offer = kind switch
        {
            "lf-short" => TestData.Offer("D-LF", "D", CourseType.Lf) with { Schedule = [new(1, 5), new(2, 5), new(3, 5)] },
            "psy-bf" => TestData.Offer("PSY-BF", "PSY") with { Schedule = [new(1, 2), new(2, 2)] },
            "psy-hj3" => TestData.Offer("PSY-WF", "PSY", CourseType.Elective) with { Schedule = [new(1, 2), new(3, 2)] },
            "psy-hj1" => TestData.Offer("PSY-WF", "PSY", CourseType.Elective) with { Schedule = [new(1, 2)] },
            "sia-2h" => TestData.Offer("SIA-SK", "SIA", CourseType.Seminar) with { Schedule = [new(1, 2), new(2, 2)] },
            "geo-hj1" => TestData.Offer("GEO-BF", "GEO") with { Schedule = [new(1, 2), new(3, 2), new(4, 2)] },
            "geo-wf" => TestData.Offer("GEO-WF", "GEO", CourseType.Elective) with { Schedule = [new(3, 2), new(4, 2)] },
            "inf-no-req" => TestData.Offer("INF-BF", "INF"),
            "inf-2h" => TestData.Offer("INF-BF", "INF") with { Schedule = [new(1, 2), new(2, 2), new(3, 2), new(4, 2)], Requirements = [Requirement.Informatics] },
            "i-no-req" => TestData.Offer("I-BF", "I"),
            "i-wf" => TestData.Offer("I-WF", "I", CourseType.Elective) with { Requirements = [Requirement.ItalianProfile] },
            "bili-no-req" => TestData.Offer("BIO-BF-BILI", "BIO") with { Variant = CourseVariant.Bilingual },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var violation = Assert.Single(CatalogRules.Check(offer));
        Assert.Equal(ruleId, violation.Rule.Id); Assert.Equal(ReviewStatus.Invalid, violation.Rule.Severity);
        Assert.False(validator.GetAvailability(offer, ProfileContext.Missing).CanSelect);

        var catalog = TestData.Catalog with { Subjects = [new(offer.SubjectId, offer.SubjectId)], Offers = [offer] };
        var result = validator.Validate(TestData.Choice with { Selections = [TestData.Select(offer)] }, catalog, ProfileContext.Missing);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == ruleId);
    }
    [Fact] public void ValidOfferHasNoCatalogViolation() => Assert.Empty(CatalogRules.Check(TestData.Offer()));

    [Fact] public void ValidateReportsMissingProfileAndUnknownPrerequisite()
    {
        var offer = TestData.Offer() with { Requirements = [Requirement.Informatics] };
        var result = validator.Validate(TestData.Choice with { Selections = [TestData.Select(offer)] }, TestData.Catalog with { Offers = [offer] }, ProfileContext.Missing);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == "PROFILE-TRUST");
        Assert.Contains(result.Messages, m => m.Rule.Id == "PREREQUISITE-UNKNOWN" && m.SubjectIds.Contains("D"));
    }
    [Fact] public void ValidateReportsUnmetPrerequisiteAsInvalid()
    {
        var offer = TestData.Offer() with { Requirements = [Requirement.Informatics] };
        var profile = new ProfileContext(ProfileStatus.Verified, PriorEducation.Unknown with { InformaticsBridge = Knowledge.No, InformaticsImp = Knowledge.No });
        var result = validator.Validate(TestData.Choice with { Selections = [TestData.Select(offer)] }, TestData.Catalog with { Offers = [offer] }, profile);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == "PREREQUISITE-NO");
        Assert.DoesNotContain(result.Messages, m => m.Rule.Id == "PROFILE-TRUST");
        Assert.Contains(result.Messages, m => m.Rule.Id == "M2-RELEASE");
    }
    [Fact] public void ValidateReportsCancelledOfferAsInvalid()
    {
        var offer = TestData.Offer() with { Status = OfferStatus.Cancelled };
        var result = validator.Validate(TestData.Choice with { Selections = [TestData.Select(offer)] }, TestData.Catalog with { Offers = [offer] }, ProfileContext.Missing);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == "OFFER-SELECTION");
    }
    [Fact] public void ValidateReportsPartialSeminarSelectionAsInvalid()
    {
        // Pflichtenheft §4, §6 Nr.12: SIA umfasst zwei Seminarkurs-Halbjahre HJ 1–2.
        var sia = TestData.Offer("SIA-SK", "SIA", CourseType.Seminar) with { Schedule = [new(1, 3), new(2, 3)] };
        var catalog = TestData.Catalog with { Subjects = [new("SIA", "SIA")], Offers = [sia] };
        var choice = TestData.Choice with { Selections = [new("SIA-SK", "SIA", CourseType.Seminar, CourseVariant.Normal, [1])] };
        var result = validator.Validate(choice, catalog, ProfileContext.Missing);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == "OFFER-SELECTION");
    }
    [Fact] public void ValidateReportsInvalidPlannedOralSubjectAsInvalid()
    {
        // Pflichtenheft §6 Nr.8,11: GEO oder bereits als LF belegtes Fach nicht als mündliche Prüfung zulässig.
        var result = validator.Validate(TestData.Choice with { PlannedOralSubjects = ["GEO"] }, TestData.WorkflowCatalog, ProfileContext.Missing);
        Assert.Equal(ReviewStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Rule.Id == "PLAN-ORAL");
    }
}
