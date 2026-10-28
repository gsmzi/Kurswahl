using System.Text;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.ImportExport.Tests;

public sealed class ChoiceTableTests
{
    private static Dictionary<string, string> Mapping => ChoiceTableImport.Fields.ToDictionary(f => f);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportRoundTripPreservesVariantsAndSemesters(bool xlsx)
    {
        var catalog = TestData.WorkflowCatalog;
        var choice = TestData.Choice with { Selections = [TestData.Select(catalog.Offers.Single(o => o.Id == "D-LF")), TestData.Select(catalog.Offers.Single(o => o.Id == "GEO-BF"))] };
        var student = new StudentRecord(1, new(Guid.NewGuid(), choice.StudentId, "Demo Export", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", false, 1, "demo-test", DateTimeOffset.UtcNow, null);
        var revision = new ChoiceRevision(Guid.NewGuid(), null, choice.StudentId, DateTimeOffset.UtcNow, "demo-test", ActorRole.Teacher, RevisionSource.TeacherEdit, "Test", catalog.Version, choice.RuleVersion, choice, [], null);
        var table = TableExport.Choices([new(student, revision, new(ReviewStatus.NeedsReview, []), false, null, "")]);
        var imported = xlsx ? StudentTableImport.Xlsx(new MemoryStream(TableExport.Xlsx(table))) : StudentTableImport.Csv(Encoding.UTF8.GetString(TableExport.Csv(table)));
        var result = Assert.Single(ChoiceTableImport.Read(imported, Mapping, catalog));
        Assert.Equal(2, result.Selections.Length);
        Assert.Equal(new[] { 3, 4 }, result.Selections.Single(s => s.SubjectId == "GEO").Semesters);
        Assert.All(imported.Rows, r => Assert.Contains("needs_review", r));
    }
    [Theory]
    [InlineData("demo-001;2028;D;Lf;Normal;1")]
    [InlineData("demo-001;2029;D;Lf;;1")]
    [InlineData("demo-001;2029;D;Lf;Normal;5")]
    [InlineData("demo-001;2029;UNKNOWN;Lf;Normal;1")]
    [InlineData("demo-001;2029;D;99;Normal;1")]
    [InlineData(";2029;D;Lf;Normal;1")]
    public void MalformedRowsRejectEntirePreview(string row)
    {
        var table = StudentTableImport.Csv("studentId;year;subject;type;variant;semester\ndemo-002;2029;D;Lf;Normal;1\n" + row);
        Assert.Throws<InvalidDataException>(() => ChoiceTableImport.Read(table, Mapping, TestData.WorkflowCatalog));
    }
    [Fact]
    public void DuplicateRowsAndAmbiguousIdentityAreRejected()
    {
        foreach (var id in new[] { "demo-a", "demo-A" })
        {
            var table = StudentTableImport.Csv($"studentId;year;subject;type;variant;semester\ndemo-a;2029;D;Lf;Normal;1\n{id};2029;D;Lf;Normal;1");
            Assert.Throws<InvalidDataException>(() => ChoiceTableImport.Read(table, Mapping, TestData.WorkflowCatalog));
        }
    }
    [Fact]
    public void SpreadsheetFormulasAreTextAndCsvIsEscaped()
    {
        var table = new ExportTable(["value"], [["=1+1"], ["  @SUM(1)"], ["a;\"b\"\nc"]]);
        var csv = StudentTableImport.Csv(Encoding.UTF8.GetString(TableExport.Csv(table)));
        Assert.Equal("'=1+1", csv.Rows[0][0]); Assert.Equal("'  @SUM(1)", csv.Rows[1][0]); Assert.Equal(table.Rows[2][0], csv.Rows[2][0]);
        var xlsx = StudentTableImport.Xlsx(new MemoryStream(TableExport.Xlsx(table)));
        Assert.Equal("=1+1", xlsx.Rows[0][0]);
    }
}
