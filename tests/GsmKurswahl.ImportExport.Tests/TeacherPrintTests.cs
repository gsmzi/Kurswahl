using System.Text;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.ImportExport.Tests;

// Pflichtenheft §11/§12: printable control sheets and lists from the teacher stock. Synthetic persons only.
public class TeacherPrintTests
{
    private static StudentRecord Record(int number) => new(SchemaVersions.StudentRecord,
        new(Guid.NewGuid(), $"demo-{number:000}", $"Testperson {number:000}, Demo", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown),
        "", "", "", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null);
    private static CourseChoice Choice(int number) => TestData.Choice with
    {
        StudentId = $"demo-{number:000}", PlannedOralSubjects = ["D"],
        Selections = [TestData.Select(TestData.Offer())]
    };
    private static int Pages(byte[] pdf) => System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;
    private static void AssertPdf(byte[] bytes)
    {
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.AsSpan(0, 8)));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 64))));
    }

    [Fact] public void ControlSheetsPutEveryPupilOnAnOwnPageIncludingPupilsWithoutChoice()
    {
        var items = new[]
        {
            new ControlSheetItem(Record(1), Choice(1), ["Geschichte: Pflichtbelegung fehlt."], ["Sportbefreiung und Ersatzbedarf sind unbekannt."]),
            new ControlSheetItem(Record(2), Choice(2), [], []),
            new ControlSheetItem(Record(3), null, [], [])
        };
        var bytes = PlanningPrint.ControlSheets(items, TestData.Catalog);
        AssertPdf(bytes);
        Assert.Equal(3, Pages(bytes));
        Assert.Throws<ArgumentException>(() => PlanningPrint.ControlSheets([], TestData.Catalog));
    }

    [Fact] public void OverviewAndCourseListsBreakLongTablesAcrossPages()
    {
        var rows = Enumerable.Range(1, 90).Select(i => new[] { $"Testperson {i:000}, Demo", "10a", "D · M · E", "G, SP, GK (HJ 1,2), GEO (HJ 3,4), BK, ETH, BIO, CH", "BIO, G", "Fachlich zu klären" }).ToArray();
        var overview = PlanningPrint.Overview(2029, "Abitur 2029 · alle Personen", rows);
        AssertPdf(overview);
        Assert.True(Pages(overview) >= 3);
        var members = Enumerable.Range(1, 5).Select(i => new[] { i.ToString(), $"Testperson {i:000}, Demo", "10a", "HJ 1–4" }).ToArray();
        var lists = PlanningPrint.CourseLists(2029, [new("Physik LF", members), new("Psychologie Wahlfach · HJ 1", members)]);
        AssertPdf(lists);
        Assert.Equal(2, Pages(lists));
        Assert.Throws<ArgumentException>(() => PlanningPrint.CourseLists(2029, []));
    }

    [Fact] public void ReselectionSheetWithoutCancelledCoursePrintsTheOpenPoints()
    {
        var withNotes = new ReselectionPrintItem(Record(1), Choice(1), []) { Notes = ["Geschichte: Pflichtbelegung fehlt."] };
        var cancelled = new ReselectionPrintItem(Record(2), Choice(2), ["D-BF"]);
        var bytes = PlanningPrint.Reselections([withNotes, cancelled], TestData.Catalog);
        AssertPdf(bytes);
        Assert.Equal(2, Pages(bytes));
    }
}
