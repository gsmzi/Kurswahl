using System.IO.Compression;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport.Tests;

public class StudentTableImportTests
{
    private static ImportOrigin Origin => new("demo.csv", "synthetic-hash", "v1", DateTimeOffset.UtcNow, "demo-beratung");
    [Theory]
    [InlineData("accountId", "S-1-5-21-100-200-300-1")]
    [InlineData("home", "10a/Muster.Demo")]
    public void DuplicateAccountIdsAndDirectoriesAreExcludedFromBatch(string field, string value)
    {
        var table = StudentTableImport.Csv($"studentId;name;class;year;username;{field}\ndemo-001;Demo Eins;10a;2029;DEMO\\test1;{value}\ndemo-002;Demo Zwei;10a;2029;DEMO\\test2;{value}");
        var rows = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), Origin, "demo-beratung");
        Assert.All(rows, row => Assert.NotEmpty(row.Error));
    }
    [Theory]
    [InlineData("", "10a/Muster.Demo", false)]
    [InlineData("Muster.Demo2", "10a/Muster.Demo2", false)]
    [InlineData("../anderer", "", true)]
    public void NamesBuildDefaultFolderAndOptionalOverrideIsValidated(string folder, string expected, bool error)
    {
        var table = StudentTableImport.Csv("studentId;firstName;lastName;class;year;username;folder\ndemo-001;Demo;Muster;10a;2029;DEMO\\demo;" + folder);
        var row = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), Origin, "demo-beratung").Single();
        if (error) Assert.NotEmpty(row.Error);
        else { Assert.Empty(row.Error); Assert.Equal(expected, row.Candidate!.HomeDirectory); Assert.Equal("Demo Muster", row.Candidate.Student.SyntheticDisplayName); }
    }
    [Fact]
    public void ShippedDemoImportsWithoutErrorsAndLeavesMappingUnconfirmed()
    {
        var table = StudentTableImport.Csv(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "klasse10-demo.csv")));
        var rows = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), Origin, "demo-beratung");
        Assert.Equal(2, rows.Length);
        Assert.All(rows, r => { Assert.Empty(r.Error); Assert.NotNull(r.Candidate); Assert.False(r.Candidate.MappingConfirmed); });
        Assert.Equal(DepartureStatus.ExpectedDeparture, rows[1].Candidate!.Student.Departure);
        Assert.Equal(Knowledge.Unknown, rows[1].Candidate!.Student.Prerequisites.InformaticsImp);
    }
    [Fact]
    public void CsvMappingPreservesUnknownsAndReportsDuplicateIdentities()
    {
        var table = StudentTableImport.Csv("Kennung;Name;Klasse;Jahr;Konto;IMP\r\ndemo-001;\"Demo; Person\";10a;2029;DEMO\\test1;\r\ndemo-001;Demo Zwei;10a;2029;DEMO\\test2;ja");
        var mapping = new Dictionary<string, string> { ["studentId"] = "Kennung", ["name"] = "Name", ["class"] = "Klasse", ["year"] = "Jahr", ["username"] = "Konto", ["imp"] = "IMP" };
        var preview = StudentTableImport.Preview(table, mapping, Origin, "demo-beratung");
        Assert.Equal("Demo; Person", preview[0].Candidate!.Student.SyntheticDisplayName);
        Assert.Equal(Knowledge.Unknown, preview[0].Candidate!.Student.Prerequisites.InformaticsImp);
        Assert.Equal(Knowledge.Yes, preview[1].Candidate!.Student.Prerequisites.InformaticsImp);
        Assert.All(preview, row => Assert.NotEmpty(row.Error)); Assert.False(preview[0].Candidate!.MappingConfirmed);
    }
    [Fact]
    public void InvalidValuesDoNotBecomeFalseAndMalformedCsvFails()
    {
        var table = StudentTableImport.Csv("studentId;name;class;year;username;imp\ndemo-001;Demo;10a;2029;DEMO\\test1;vielleicht");
        var preview = StudentTableImport.Preview(table, table.Headers.ToDictionary(h => h, h => h), Origin, "demo-beratung");
        Assert.Null(preview[0].Candidate); Assert.Contains("Wahrheitswert", preview[0].Error);
        Assert.Throws<InvalidDataException>(() => StudentTableImport.Csv("a;b\n\"unclosed"));
        Assert.Throws<InvalidDataException>(() => StudentTableImport.Csv("a;a\n1;2"));
    }
    [Fact]
    public void XlsxReadsSparseInlineCellsAndRejectsFormulas()
    {
        static MemoryStream Workbook(bool formula)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                void Add(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
                Add("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Demo\" r:id=\"r1\"/></sheets></workbook>");
                Add("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id=\"r1\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                Add("xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row><c r=\"A1\" t=\"inlineStr\"><is><t>name</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>year</t></is></c></row><row><c r=\"B2\">" + (formula ? "<f>2000+29</f>" : "") + "<v>2029</v></c></row></sheetData></worksheet>");
            }
            stream.Position = 0; return stream;
        }
        using var source = Workbook(false); var table = StudentTableImport.Xlsx(source);
        Assert.Equal("", table.Rows[0][0]); Assert.Equal("2029", table.Rows[0][1]);
        using var formulas = Workbook(true); Assert.Throws<InvalidDataException>(() => StudentTableImport.Xlsx(formulas));
    }
}
