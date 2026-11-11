using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;
using GsmKurswahl.Tests;

namespace GsmKurswahl.Storage.Tests;

// Filters and course lists of the teacher's choice page (Nutzerauftrag 11.11.2026). Synthetic persons only.
public class ChoiceOverviewTests
{
    private static readonly OfferCatalog Catalog = TestData.WorkflowCatalog;
    private static readonly string[] Complete = ["D-LF", "M-LF", "E-LF", "BIO-BF", "CH-BF", "G-BF", "BK-BF", "ETH-BF", "SP-BF", "GK-BF", "GEO-BF"];

    private static TeacherState State(int number, string className, string[]? offers, string[]? oral = null, bool reselection = false, string? withoutOffer = null)
    {
        var prerequisites = PriorEducation.Unknown with { SportExemptSemesters = [], PermanentSportExemption = Knowledge.No, ReligionEthicsGrade10 = "ETH",
            ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.Yes };
        var record = new StudentRecord(SchemaVersions.StudentRecord,
            new(Guid.NewGuid(), $"demo-{number:000}", $"Testperson {number:000}, Demo", className, 2029, DepartureStatus.Active, prerequisites),
            "", "", "", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null);
        if (offers is null) return new(record, null, null, reselection, null, "");
        var choice = TestData.Choice with { StudentId = record.Student.ExternalStudentId, OfferVersion = Catalog.Version, PlannedOralSubjects = [.. oral ?? ["BIO", "G"]],
            Selections = [.. offers.Where(id => id != withoutOffer).Select(id => TestData.Select(Catalog.Offers.Single(o => o.Id == id)))] };
        var revision = new ChoiceRevision(Guid.NewGuid(), null, choice.StudentId, DateTimeOffset.UtcNow, "demo-beratung", ActorRole.Teacher,
            RevisionSource.TeacherEdit, "Demo", choice.OfferVersion, choice.RuleVersion, choice, [], null);
        var validation = ChoiceOverview.Validate(choice, Catalog, record);
        return new(record, revision, validation, reselection || validation.Status == ReviewStatus.Invalid, null, "");
    }

    private static TeacherState[] States =>
    [
        State(1, "10a", Complete),
        State(2, "10a", Complete, withoutOffer: "G-BF"),      // required course missing
        State(3, "10b", Complete, oral: []),                  // oral plan missing
        State(4, "10b", null),                                // no choice yet
        State(5, "10c", Complete, reselection: true)
    ];
    private static string[] Ids(IEnumerable<TeacherState> states) => [.. states.Select(s => s.Record.Student.ExternalStudentId)];

    [Fact] public void ClassesComeFromTheStockAndFilterByFormerClass()
    {
        Assert.Equal(new[] { "10a", "10b", "10c" }, ChoiceOverview.Classes(States));
        Assert.Equal(new[] { "demo-003", "demo-004" }, Ids(ChoiceOverview.Apply(States, new(ClassName: "10b"))));
        Assert.Equal(new[] { "demo-005" }, Ids(ChoiceOverview.Apply(States, new(Text: "person 005"))));
    }

    [Fact] public void StateFiltersSeparateIncompleteViolationAndReselection()
    {
        Assert.Equal(new[] { "demo-004" }, Ids(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.WithoutChoice))));
        // Incomplete: missing required course, missing oral plan, no choice at all.
        Assert.Equal(new[] { "demo-002", "demo-003", "demo-004" }, Ids(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.Incomplete))));
        Assert.Equal(new[] { "demo-002", "demo-003" }, Ids(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.Violation))));
        Assert.Equal(new[] { "demo-002", "demo-003", "demo-005" }, Ids(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.ReselectionOpen))));
        Assert.DoesNotContain("demo-002", Ids(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.Valid))));
        Assert.All(ChoiceOverview.Apply(States, new(State: ChoiceStateFilter.NeedsReview)), s => Assert.Equal(ReviewStatus.NeedsReview, s.Validation!.Status));
        Assert.NotEmpty(ChoiceOverview.Violations(States[1].Validation));
    }

    [Fact] public void CourseFilterAndCourseListUseOfferAndSemester()
    {
        Assert.Equal(new[] { "demo-001", "demo-003", "demo-005" }, Ids(ChoiceOverview.Apply(States, new(OfferId: "G-BF"))));
        // GEO is only taught in HJ 3+4.
        Assert.Empty(ChoiceOverview.Apply(States, new(OfferId: "GEO-BF", Semester: 1)));
        Assert.Equal(4, ChoiceOverview.Apply(States, new(OfferId: "GEO-BF", Semester: 3)).Length);
        var list = ChoiceOverview.Members(States, Catalog, "G-BF");
        Assert.Equal(new[] { "demo-001", "demo-003", "demo-005" }, list.Members.Select(m => m.Student.Student.ExternalStudentId));
        Assert.Equal(new[] { 1, 2, 3, 4 }, list.Members[0].Semesters);
        Assert.EndsWith("· HJ 3", ChoiceOverview.Members(States, Catalog, "GEO-BF", 3).Title);
        var all = ChoiceOverview.AllCourseLists(States, Catalog);
        Assert.Contains(all, l => l.Offer.Id == "D-LF" && l.Members.Length == 4);
        Assert.DoesNotContain(all, l => l.Members.IsEmpty);
    }
}
