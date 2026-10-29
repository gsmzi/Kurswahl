using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Rules.Tests;

// Pflichtenheft §6; AGVO §§12/13/17/21. These are structural checks, not legal release.
public class AcademicRulesTests
{
    [Theory]
    [InlineData(36, false)]
    [InlineData(37, false)]
    [InlineData(38, true)]
    public void UpperHoursWarningUsesStrictSemesterThreshold(int hours, bool warning)
    {
        // Nutzerfestlegung 29.10.2026: Hinweis, keine automatische Ablehnung.
        var offers = new[] { "D", "M", "E", "G" }.Select(s => TestData.Offer(s + "-BF", s)
            with { Schedule = [new(1, 9), new(2, 9), new(3, 9), new(4, 9)] }).ToList();
        if (hours > 36) offers.Add(TestData.Offer("BIO-BF", "BIO") with { Schedule = [new(1, hours - 36), new(3, hours - 36)] });
        var catalog = TestData.WorkflowCatalog with { Offers = [.. offers] };
        var choice = TestData.Choice with { Selections = [.. offers.Select(TestData.Select)] };
        var messages = Check(choice, catalog: catalog).Messages.Where(m => m.Rule.Id == "GSM-HOURS-UPPER").ToArray();
        Assert.Equal(warning, messages.Length > 0);
        if (warning)
        {
            Assert.Equal(new[] { 1, 3 }, messages[0].Semesters);
            Assert.Equal(ReviewStatus.NeedsReview, messages[0].Rule.Severity);
            Assert.Contains("38 Wochenstunden", messages[0].Explanation);
        }
    }

    [Theory]
    [InlineData("ETH", "KR", true)]
    [InlineData("ETH", "ER", true)]
    [InlineData("KR", "ER", true)]
    [InlineData("ETH", "KR", false)]
    [InlineData("ETH", "ER", false)]
    [InlineData("KR", "ER", false)]
    public void ReligionSubjectsCannotOverlapButSequentialChangeIsNotParallel(string first, string second, bool overlap)
    {
        // Nutzerfestlegung 29.10.2026; Pflichtenheft §6 Nr.9.
        var c = Complete with { Selections = [.. Complete.Selections.Where(s => s.SubjectId != "ETH"),
            new(first + "-BF", first, CourseType.Bf, CourseVariant.Normal, [1, 2]),
            new(second + "-BF", second, CourseType.Bf, CourseVariant.Normal, overlap ? [1, 2] : [3, 4])] };
        var before = c.Selections;
        var messages = AcademicRules.Check(c, TestData.WorkflowCatalog, Profile);
        Assert.Equal(overlap, messages.Any(m => m.Rule.Id == "GSM-RELIGION-EXCLUSIVE"));
        if (overlap)
        {
            var message = Assert.Single(messages.Where(m => m.Rule.Id == "GSM-RELIGION-EXCLUSIVE"));
            Assert.Equal(new[] { 1, 2 }, message.Semesters);
            Assert.Equal(ReviewStatus.Invalid, message.Rule.Severity);
            Assert.Contains(first, message.SubjectIds); Assert.Contains(second, message.SubjectIds);
        }
        Assert.Equal(before, c.Selections);
    }

    [Fact]
    public void MutuallyExclusiveReligionSubjectsAreNeverAnOralPair()
    {
        var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new("KR", "KR")),
            Offers = TestData.WorkflowCatalog.Offers.Add(TestData.Offer("KR-BF", "KR")) };
        var c = Choice("D-LF", "M-LF", "W-LF", "ETH-BF") with { PlannedOralSubjects = ["ETH", "KR"] };
        c = c with { Selections = c.Selections.Add(TestData.Select(catalog.Offers.Single(o => o.Id == "KR-BF"))) };
        Assert.Contains("GSM-RELIGION-EXCLUSIVE", ChoiceWorkflow.OralError(c, catalog, Profile));
        Assert.DoesNotContain(AcademicRules.ExamOptions(c, catalog, Profile), o => AcademicRules.MutuallyExclusiveSubjects(o.First, o.Second));
    }

    private static ProfileContext Profile => new(ProfileStatus.Verified, PriorEducation.Unknown with
    {
        InformaticsImp = Knowledge.Yes, SportExemptSemesters = [], PermanentSportExemption = Knowledge.No,
        ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.Yes
    });
    private static CourseChoice Choice(params string[] ids) => TestData.Choice with
    { Selections = [.. ids.Select(id => TestData.Select(TestData.WorkflowCatalog.Offers.Single(o => o.Id == id)))] };
    private static ValidationResult Check(CourseChoice c, ProfileContext? p = null, OfferCatalog? catalog = null) =>
        new ChoiceValidator().Validate(c, catalog ?? TestData.WorkflowCatalog, p ?? Profile);
    private static CourseChoice Complete => Choice("D-LF", "M-LF", "E-LF", "BIO-BF", "CH-BF", "G-BF", "BK-BF", "ETH-BF", "SP-BF", "GK-BF", "GEO-BF")
        with { PlannedOralSubjects = ["BIO", "G"] };

    [Fact] public void CompleteStructuralChoiceRemainsReviewWithoutYearRelease()
    {
        var result = Check(Complete);
        Assert.Equal(ReviewStatus.NeedsReview, result.Status);
        Assert.DoesNotContain(result.Messages, m => m.Rule.Severity == ReviewStatus.Invalid);
        Assert.Contains(result.Messages, m => m.Rule.Id == "M2-RELEASE");
        Assert.NotEmpty(AcademicRules.ExamOptions(Complete, TestData.WorkflowCatalog, Profile));
    }
    [Fact] public void MissingOralSelectionIsReportedAsInvalidPlanOral() =>
        Assert.Contains(Check(Complete with { PlannedOralSubjects = [] }).Messages, m => m.Rule.Id == "PLAN-ORAL");
    [Fact] public void AtLeastTwoCoreLfAreRequired()
    {
        var c = Choice("D-LF", "W-LF");
        Assert.Contains(Check(c).Messages, m => m.Rule.Id == "AGVO-LF");
    }
    [Fact] public void MinimumNeedsThirtyNonLfCourses() =>
        Assert.Contains(Check(Choice("D-LF", "M-LF", "E-LF")).Messages, m => m.Rule.Id == "AGVO-COURSES");
    [Fact] public void MissingHistoryIsIdentifiedBySubjectAndSemesters()
    {
        var c = Complete with { Selections = [.. Complete.Selections.Where(s => s.SubjectId != "G")] };
        var message = Assert.Single(Check(c).Messages.Where(m => m.Rule.Id == "AGVO-REQUIRED" && m.SubjectIds.Contains("G")));
        Assert.Equal(new[] { 1, 2, 3, 4 }, message.Semesters);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void EconomicsChangesGkAndGeoRequirements(bool economics)
    {
        var c = economics ? Choice("E-LF", "BIO-LF", "W-LF", "D-BF", "M-BF") : Choice("D-LF", "M-LF", "E-LF");
        var required = ChoiceWorkflow.Required(c);
        Assert.Equal(economics ? new[] { 1 } : [1, 2], required.Single(r => r.SubjectId == "GK").Semesters);
        Assert.Equal(economics ? new[] { 3 } : [3, 4], required.Single(r => r.SubjectId == "GEO").Semesters);
        Assert.Equal(economics ? new[] { 1, 3, 4 } : [1, 2, 3, 4], ChoiceWorkflow.OralSemesters(c, "GK"));
    }

    [Theory]
    [InlineData(false, "GK")]
    [InlineData(true, "GK")]
    [InlineData(false, "GEO")]
    [InlineData(true, "GEO")]
    [InlineData(false, "BK")]
    [InlineData(true, "BK")]
    public void AllSemesterSubsetsRespectSchoolYearsAndExactEconomicsException(bool economics, string subject)
    {
        // Pflichtenheft §§4,6 Nr.12; AGVO §10; Nutzerpräzisierung 28.10.2026.
        for (var mask = 1; mask < 16; mask++)
        {
            var semesters = Enumerable.Range(1, 4).Where(h => (mask & (1 << (h - 1))) != 0).ToImmutableArray();
            var c = economics ? Choice("D-LF", "M-LF", "W-LF") : Choice("D-LF", "M-LF", "E-LF");
            c = c with { Selections = c.Selections.Add(new(subject + "-BF", subject, CourseType.Bf, CourseVariant.Normal, semesters)) };
            var requiresFirstPair = !(economics && subject == "GK");
            var requiresSecondPair = !(economics && subject == "GEO");
            var invalid = (requiresFirstPair && semesters.Contains(1) != semesters.Contains(2))
                || (requiresSecondPair && semesters.Contains(3) != semesters.Contains(4));
            var issues = Check(c).Messages.Where(m => m.Rule.Id == "AGVO-SEMESTERS" && m.SubjectIds.Contains(subject)).ToArray();
            Assert.Equal(invalid, issues.Length > 0);
            if (invalid)
            {
                Assert.Equal(ReviewStatus.Invalid, issues[0].Rule.Severity);
                Assert.Equal(semesters.ToArray(), issues[0].Semesters.ToArray());
            }
        }
    }

    [Fact]
    public void InvalidGkSelectionIsReportedWithoutChangingImportedChoice()
    {
        var c = Choice("D-LF", "M-LF", "W-LF", "GK-BF");
        c = c with { Selections = [.. c.Selections.Select(s => s.SubjectId == "GK" ? s with { Semesters = [1, 4] } : s)] };
        var before = System.Text.Json.JsonSerializer.Serialize(c, DocumentJson.Options);
        Assert.Contains(Check(c).Messages, m => m.Rule.Id == "AGVO-SEMESTERS" && m.SubjectIds.Contains("GK"));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(c, DocumentJson.Options));
    }
    [Fact] public void InformaticsReplacesOnlySecondScience()
    {
        var inf = TestData.Offer("INF-BF", "INF") with { Requirements = [Requirement.Informatics] };
        var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new("INF", "Informatik")), Offers = TestData.WorkflowCatalog.Offers.Add(inf) };
        var c = Complete with { Selections = [.. Complete.Selections.Where(s => s.SubjectId != "CH"), TestData.Select(inf)] };
        Assert.DoesNotContain(Check(c, catalog: catalog).Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
        c = c with { Selections = [.. c.Selections.Where(s => s.SubjectId != "BIO")] };
        Assert.Contains(Check(c, catalog: catalog).Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
    }
    [Fact] public void ItalianProfileDoesNotProveContinuedLanguage()
    {
        var italian = TestData.Offer("I-BF", "I") with { Requirements = [Requirement.ItalianProfile] };
        var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new("I", "Italienisch")), Offers = TestData.WorkflowCatalog.Offers.Add(italian) };
        var c = Complete with { Selections = [.. Complete.Selections.Where(s => s.SubjectId != "CH"), TestData.Select(italian)] };
        var p = Profile with { Prerequisites = Profile.Prerequisites with { ItalianProfile = Knowledge.Yes } };
        Assert.Contains(Check(c, p, catalog).Messages, m => m.Rule.Id == "AGVO-LANGUAGE-REVIEW");
        p = p with { Prerequisites = p.Prerequisites with { ContinuedLanguages = ["E", "I"] } };
        Assert.DoesNotContain(Check(c, p, catalog).Messages, m => m.Rule.Id == "AGVO-LANGUAGE-SCIENCE");
        Assert.Contains(ChoiceWorkflow.Tasks(c, catalog, p), t => t.Id == "PLAN-AREA-BREADTH" && t.IsSatisfied);
    }
    [Fact] public void SportReplacementRemainsRequiredAtFortyTwoAndSiaCannotReplaceIt()
    {
        var sia = TestData.Offer("SIA-SK", "SIA", CourseType.Seminar) with { Schedule = [new(1, 3), new(2, 3)] };
        var catalog = TestData.WorkflowCatalog with { Subjects = TestData.WorkflowCatalog.Subjects.Add(new("SIA", "SIA")), Offers = TestData.WorkflowCatalog.Offers.Add(sia) };
        var c = Complete with { PlannedOralSubjects = ["GK", "ETH"], Selections = [.. Complete.Selections.Select(s => s.SubjectId == "SP"
            ? s with { Semesters = [3, 4] } : s), TestData.Select(sia)] };
        Assert.Equal(42, c.Selections.Sum(s => s.Semesters.Length));
        var p = Profile with { Prerequisites = Profile.Prerequisites with { SportExemptSemesters = [1, 2] } };
        Assert.Contains(Check(c, p, catalog).Messages, m => m.Rule.Id == "AGVO-SPORT-REPLACEMENT");
        var psy = TestData.Offer("PSY-WF", "PSY", CourseType.Elective) with { Schedule = [new(1, 2), new(2, 2)] };
        catalog = catalog with { Subjects = catalog.Subjects.Add(new("PSY", "Psychologie")), Offers = catalog.Offers.Add(psy) };
        c = c with { Selections = c.Selections.Add(TestData.Select(psy)) };
        Assert.DoesNotContain(Check(c, p, catalog).Messages, m => m.Rule.Id == "AGVO-SPORT-REPLACEMENT");
    }
    [Fact] public void SportExemptionBlocksSportAsOralCandidateAndAllowsNoSingleSemesterReplacement()
    {
        // Nutzerfestlegung 29.10.2026: at course selection replacement courses are complete school years.
        var p = Profile with { Prerequisites = Profile.Prerequisites with { SportExemptSemesters = [1] } };
        Assert.DoesNotContain("SP", ChoiceWorkflow.OralCandidates(Complete, TestData.WorkflowCatalog, p));
        var sportReduced = Complete with { Selections = [.. Complete.Selections.Select(s => s.SubjectId == "SP" ? s with { Semesters = [2, 3, 4] } : s)] };
        Assert.DoesNotContain(Check(sportReduced, p).Messages, m => m.Rule.Id == "AGVO-SEMESTERS");
        var oneSingle = sportReduced with { Selections = sportReduced.Selections.Add(new("MU-BF", "MU", CourseType.Bf, CourseVariant.Normal, [1])) };
        Assert.Contains(Check(oneSingle, p).Messages, m => m.Rule.Id == "AGVO-SEMESTERS" && m.SubjectIds.Contains("MU"));
        var fullYear = sportReduced with { Selections = sportReduced.Selections.Add(new("MU-BF", "MU", CourseType.Bf, CourseVariant.Normal, [1, 2])) };
        Assert.DoesNotContain(Check(fullYear, p).Messages, m => m.Rule.Id == "AGVO-SEMESTERS");
        var mu = TestData.WorkflowCatalog.Offers.Single(o => o.Id == "MU-BF");
        Assert.Equal(new[] { "1,2", "3,4" }, AcademicRules.SemesterSelectionUnits(sportReduced, mu, p).Select(u => string.Join(",", u)));
    }
    [Fact] public void ReligionEthicsSwitchBlocksOralChoiceAndReportsFeststellungspruefungHint()
    {
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects, new("KR", "Katholische Religion"), new("ER", "Evangelische Religion")],
            Offers = [.. TestData.WorkflowCatalog.Offers, TestData.Offer("KR-BF", "KR"), TestData.Offer("ER-BF", "ER")]
        };
        var p = Profile with { Prerequisites = Profile.Prerequisites with { ReligionEthicsGrade10 = "KR", ReligionEthicsExamPermission = Knowledge.Unknown } };
        // Only the subject attended in Grade 10 (KR) is a regular oral candidate without explicit exam permission.
        var candidates = ChoiceWorkflow.OralCandidates(Complete, catalog, p);
        Assert.Contains("KR", candidates);
        Assert.DoesNotContain("ETH", candidates);
        Assert.DoesNotContain("ER", candidates);
        // Complete has ETH-BF selected while Grade 10 was KR -> switch warning with Feststellungsprüfung hint.
        var result = Check(Complete, p, catalog);
        var relMsg = Assert.Single(result.Messages.Where(m => m.Rule.Id == "AGVO-RELIGION"));
        Assert.Contains("Feststellungsprüfung", relMsg.Explanation);
        Assert.Contains("Austritt aus Religionsunterricht muss beantragt werden.", relMsg.Explanation);
        Assert.Contains("Für mündliche Prüfung in Ethik ist Feststellungsprüfung notwendig.", relMsg.Explanation);
        // Planning ETH as oral subject without exam permission triggers PLAN-ORAL with Feststellungsprüfung explanation.
        var withEthOral = Complete with { PlannedOralSubjects = ["ETH", "BIO"] };
        var oralMsg = Assert.Single(Check(withEthOral, p, catalog).Messages.Where(m => m.Rule.Id == "PLAN-ORAL"));
        Assert.Contains("Feststellungsprüfung", oralMsg.Explanation);
    }

    [Theory]
    [InlineData("KR")]
    [InlineData("ER")]
    [InlineData("ETH")]
    public void ContinuedGradeTenSubjectNeedsNoAdditionalBlanketPermission(string subject)
    {
        // Pflichtenheft §6 Nr.9; Nutzerpräzisierung 28.10.2026: Fortführung ist kein Wechsel.
        var offer = TestData.Offer(subject + "-BF", subject);
        var catalog = TestData.WorkflowCatalog with
        {
            Subjects = [.. TestData.WorkflowCatalog.Subjects.Where(s => s.Id != subject), new(subject, subject)],
            Offers = [.. TestData.WorkflowCatalog.Offers.Where(o => o.Id != offer.Id), offer]
        };
        var c = Complete with { Selections = [.. Complete.Selections.Where(s => s.SubjectId != "ETH"), TestData.Select(offer)] };
        var p = Profile with { Prerequisites = Profile.Prerequisites with
        { ReligionEthicsGrade10 = subject, ReligionEthicsPermission = Knowledge.Unknown, ReligionEthicsExamPermission = Knowledge.Unknown } };
        Assert.DoesNotContain(Check(c, p, catalog).Messages, m => m.Rule.Id == "AGVO-RELIGION");
        Assert.Contains(Check(c, p with { Prerequisites = p.Prerequisites with { ReligionEthicsPermission = Knowledge.No } }, catalog).Messages,
            m => m.Rule.Id == "AGVO-RELIGION");
        Assert.Contains(Check(c, ProfileContext.Missing, catalog).Messages, m => m.Rule.Id == "AGVO-RELIGION");
    }
    [Fact] public void IncompleteBreadthSelectionDoesNotUnderstateMinimumCredits()
    {
        var incomplete = Choice("D-LF", "M-LF", "E-LF", "G-BF", "GK-BF", "GEO-BF", "BK-BF");
        // 12 LF + 4 G + 4 GK (oral) + 2 GEO + 2 BK = 24, plus 4 first science + 4 breadth (2nd language/science) = 32 minimum credits.
        Assert.Equal(32, AcademicRules.MinimumCredits(incomplete, ["G", "GK"], Profile));
    }
    [Fact] public void SportLfRequiresConfirmedAbsenceOfExemption()
    {
        var offer = TestData.Offer("SP-LF", "SP", CourseType.Lf);
        Assert.False(new ChoiceValidator().GetAvailability(offer, ProfileContext.Missing).CanSelect);
        Assert.True(new ChoiceValidator().GetAvailability(offer, Profile).CanSelect);
        Assert.False(new ChoiceValidator().GetAvailability(offer, Profile with { Prerequisites = Profile.Prerequisites with { PermanentSportExemption = Knowledge.Yes } }).CanSelect);
    }
    [Fact] public void ReligionExamDenialDoesNotBanEthicsBf()
    {
        var p = Profile with { Prerequisites = Profile.Prerequisites with { ReligionEthicsExamPermission = Knowledge.No } };
        Assert.True(new ChoiceValidator().GetAvailability(TestData.Offer("ETH-BF", "ETH"), p).CanSelect);
        Assert.DoesNotContain("ETH", ChoiceWorkflow.OralCandidates(Complete, TestData.WorkflowCatalog, p));
    }
    [Fact] public void SingleSemesterAndUnknownSportRemainDistinctCases()
    {
        var c = Complete with { Selections = [.. Complete.Selections.Select(s => s.SubjectId == "BK" ? s with { Semesters = [1] } : s)] };
        Assert.Contains(Check(c).Messages, m => m.Rule.Id == "AGVO-SEMESTERS");
        Assert.Contains(Check(c, ProfileContext.Missing).Messages, m => m.Rule.Id == "AGVO-SPORT" && m.Rule.Severity == ReviewStatus.NeedsReview);
    }
    [Fact] public void ExamsExcludeGeoAndDoNotDoubleCountLf()
    {
        var options = AcademicRules.ExamOptions(Complete, TestData.WorkflowCatalog, Profile);
        Assert.All(options, e => { Assert.NotEqual("GEO", e.First); Assert.NotEqual("GEO", e.Second); Assert.True(e.MinimumCredits <= 40); });
        Assert.Equal(30, AcademicRules.MinimumCredits(Complete, ["BIO", "G"], Profile));
    }
    [Fact] public void MissingExamCombinationAndPlanningHintsAreReported()
    {
        var result = Check(Choice("D-LF", "M-LF", "E-LF"));
        foreach (var id in new[] { "AGVO-EXAM", "AGVO-CREDIT", "AGVO-ELECTION", "PLAN-HOURS" })
            Assert.Contains(result.Messages, m => m.Rule.Id == id);
    }
    [Fact] public void ExistingOralPlanDoesNotDistortAlternativeMinimumCredits()
    {
        var oldPlan = Complete with { PlannedOralSubjects = ["GK", "ETH"] };
        Assert.Equal(AcademicRules.MinimumCredits(Complete, ["BIO", "G"], Profile),
            AcademicRules.MinimumCredits(oldPlan, ["BIO", "G"], Profile));
    }
}

