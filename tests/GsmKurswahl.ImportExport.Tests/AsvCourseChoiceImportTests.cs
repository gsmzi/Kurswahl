using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport.Tests;

public sealed class AsvCourseChoiceImportTests
{
    private static OfferCatalog FullCatalog()
    {
        (string Id, string Name)[] subjectDefs =
        [
            ("D", "Deutsch"), ("M", "Mathematik"), ("E", "Englisch"), ("F", "Französisch"),
            ("L", "Latein"), ("I", "Italienisch"), ("BIO", "Biologie"), ("CH", "Chemie"),
            ("PH", "Physik"), ("G", "Geschichte"), ("GK", "Gemeinschaftskunde"),
            ("GEO", "Geographie"), ("W", "Wirtschaft"), ("SP", "Sport"), ("BK", "Bildende Kunst"),
            ("MU", "Musik"), ("ETH", "Ethik"), ("ER", "Evangelische Religion"),
            ("KR", "Katholische Religion"), ("INF", "Informatik"), ("PSY", "Psychologie"),
            ("SIA", "Schüler-Ingenieur-Akademie")
        ];

        var offers = ImmutableArray.CreateBuilder<CourseOffer>();
        foreach (var (id, _) in subjectDefs)
        {
            if (id == "PSY")
            {
                offers.Add(new("PSY-WF", "PSY", CourseType.Elective, CourseVariant.Normal, OfferStatus.Offered,
                    [new(1, 2), new(2, 2)], []));
                continue;
            }
            if (id == "SIA")
            {
                offers.Add(new("SIA-SK", "SIA", CourseType.Seminar, CourseVariant.Normal, OfferStatus.Offered,
                    [new(1, 3), new(2, 3)], []));
                continue;
            }
            if (id is not ("INF" or "I" or "ETH" or "ER" or "KR"))
            {
                offers.Add(new($"{id}-LF", id, CourseType.Lf, CourseVariant.Normal, OfferStatus.Offered,
                    [new(1, 5), new(2, 5), new(3, 5), new(4, 5)], []));
            }
            if (id != "W")
            {
                var hrs = id is "D" or "M" or "E" or "F" or "L" or "I" or "BIO" or "CH" or "PH" or "INF" ? 3 : 2;
                var status = id == "I" ? OfferStatus.Planned : OfferStatus.Offered;
                offers.Add(new($"{id}-BF", id, CourseType.Bf, CourseVariant.Normal, status,
                    [new(1, hrs), new(2, hrs), new(3, hrs), new(4, hrs)], []));
            }
        }
        offers.Add(new("BIO-LF-BILI", "BIO", CourseType.Lf, CourseVariant.Bilingual, OfferStatus.Offered,
            [new(1, 5), new(2, 5), new(3, 5), new(4, 5)], [Requirement.ContinuousBilingual]));

        return new(SchemaVersions.Catalog, "demo-2029-v3", 2029, true,
            [.. subjectDefs.Select(s => new Subject(s.Id, s.Name))],
            offers.ToImmutable());
    }

    [Fact]
    public void CommaCsvWithEmptyFirstHeaderParsesAllSubjectsCodesSemestersAndOralOrder()
    {
        var catalog = FullCatalog();
        var csv =
            ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\r\n" +
            "KS1,\"Muster, Alex\",demo-001,\";;E [5];1-L-X-X-X-X;;WI [5];2-L-X-X-X-X;BIO [5];3-L-X-X-X-X;D [3];m2-P-X-X-X-X;MUS [2];x-p-X-X-X-X;G [2];x-p-X-X-X-X;GK [2];f-p-X------;GEO [2];f-p-----X--;REV [2];m1-p-X-X-X-X;M [3];x-p-X-X-X-X;PSYCH [2];a-p-X-X----;SPO [2];f-p-----X-X;INF [3];x-p-X-X-X-X;;\"\r\n" +
            "KS1,\"Test, Jamie\",11111111-2222-3333-4444-555555555555,\"D [5];1-L-X-X-X-X;F [5];2-L-X-X-X-X;M [5];3-L-X-X-X-X;L [3];x-p-X-X-X-X;I [3];x-p-X-X-X-X;BK [2];x-p-X-X-X-X;G [2];x-p-X-X-X-X;GK [2];z-P-X-X-Z-Z;GEO [2];b-p-----X-X;RRK [2];x-p-X-X-X-X;ETH [2];x-p-X-X-X-X;CH [3];x-p-X-X-X-X;PH [3];x-p-X-X-X-X;SPO [2];x-p-X-X-X-X\"";

        var result = AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(csv), "faecherwahl.csv", catalog);

        Assert.Empty(result.MissingIdentities);
        Assert.Equal(2, result.Choices.Length);

        var first = result.Choices[0];
        Assert.Equal("demo-001", first.StudentId);
        Assert.Equal("Muster, Alex", first.DisplayName);
        Assert.Equal("KS1", first.ClassName);
        Assert.True(first.CompareOralSubjects);
        Assert.True(first.IgnoreBilingualVariant);
        Assert.Empty(first.Warnings);
        Assert.Equal(new[] { "ER", "D" }, first.PlannedOralSubjects); // m1 (REV->ER) before m2 (D)

        Assert.Contains(first.Selections, s => s.OfferId == "W-LF" && s.Semesters.SequenceEqual([1, 2, 3, 4]));
        Assert.Contains(first.Selections, s => s.OfferId == "MU-BF" && s.Semesters.SequenceEqual([1, 2, 3, 4]));
        Assert.Contains(first.Selections, s => s.OfferId == "GK-BF" && s.Semesters.SequenceEqual([1]));
        Assert.Contains(first.Selections, s => s.OfferId == "GEO-BF" && s.Semesters.SequenceEqual([3]));
        Assert.Contains(first.Selections, s => s.OfferId == "ER-BF" && s.Semesters.SequenceEqual([1, 2, 3, 4]));
        Assert.Contains(first.Selections, s => s.OfferId == "PSY-WF" && s.Type == CourseType.Elective && s.Semesters.SequenceEqual([1, 2]));
        Assert.Contains(first.Selections, s => s.OfferId == "SP-BF" && s.Semesters.SequenceEqual([3, 4]));
        Assert.Contains(first.Selections, s => s.OfferId == "INF-BF" && s.Semesters.SequenceEqual([1, 2, 3, 4]));

        var second = result.Choices[1];
        Assert.Equal("11111111-2222-3333-4444-555555555555", second.StudentId);
        Assert.Empty(second.PlannedOralSubjects); // z-P-X-X-Z-Z is NOT an oral exam marker!
        Assert.Contains(second.Selections, s => s.OfferId == "GK-BF" && s.Semesters.SequenceEqual([1, 2, 3, 4]));
        Assert.Contains(second.Selections, s => s.OfferId == "GEO-BF" && s.Semesters.SequenceEqual([3, 4]));
        Assert.Contains(second.Selections, s => s.OfferId == "KR-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "I-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "L-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "F-LF");
        Assert.Contains(second.Selections, s => s.OfferId == "CH-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "PH-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "BK-BF");
        Assert.Contains(second.Selections, s => s.OfferId == "ETH-BF");
    }

    [Fact]
    public void SemicolonCsvAndLatin1EncodingAreSupported()
    {
        var catalog = FullCatalog();
        var csv =
            ";Name mit Vornamen;ASV-ID;Fächer Kursstufe\r\n" +
            "KS1;\"Beispiel, Kim\";demo-002;\"D [5];1-L-X-X-X-X;M [3];m1-p-X-X-X-X\"";
        var latin1Bytes = Encoding.Latin1.GetBytes(csv);

        var result = AsvCourseChoiceImport.Read(latin1Bytes, "export.csv", catalog);
        var person = Assert.Single(result.Choices);
        Assert.Equal("demo-002", person.StudentId);
        Assert.Equal(new[] { "M" }, person.PlannedOralSubjects);
        Assert.Equal(2, person.Selections.Length);
    }

    [Fact]
    public void XlsxWithEmptyFirstHeaderSharedStringsInlineAndBlankRowWorks()
    {
        var catalog = FullCatalog();
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(text);
            }
            Add("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Export\" r:id=\"r1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id=\"r1\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add("xl/sharedStrings.xml", "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>Name mit Vornamen</t></si><si><t>ASV-ID</t></si><si><t>Fächer Kursstufe</t></si><si><t>KS1</t></si><si><t>Demo, Pat</t></si></sst>");
            Add("xl/worksheets/sheet1.xml",
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                "<row r=\"1\"><c r=\"B1\" t=\"s\"><v>0</v></c><c r=\"C1\" t=\"s\"><v>1</v></c><c r=\"D1\" t=\"s\"><v>2</v></c></row>" +
                "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>3</v></c><c r=\"B2\" t=\"s\"><v>4</v></c><c r=\"C2\" t=\"inlineStr\"><is><t>demo-009</t></is></c><c r=\"D2\" t=\"inlineStr\"><is><t>D [5];1-L-X-X-X-X;BIO [5];2-L-X-X-X-X;G [2];m1-p-X-X-X-X</t></is></c></row>" +
                "<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>   </t></is></c></row>" +
                "</sheetData></worksheet>");
        }

        var result = AsvCourseChoiceImport.Read(stream.ToArray(), "zz-faecherwahl.xlsx", catalog);
        var item = Assert.Single(result.Choices);
        Assert.Equal("demo-009", item.StudentId);
        Assert.Equal("KS1", item.ClassName);
        Assert.Equal("Demo, Pat", item.DisplayName);
        Assert.Equal(new[] { "G" }, item.PlannedOralSubjects);
        Assert.Equal(3, item.Selections.Length);
    }

    [Fact]
    public void UnknownSubjectProducesWarningAndMissingSingleIdProducesMissingIdentity()
    {
        var catalog = FullCatalog();
        var csv =
            ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\n" +
            "KS1,\"Valid, Sam\",demo-001,\"D [5];1-L-X-X-X-X;SEM [3];a-p-X-X----\"\n" +
            "KS1,\"Ohne, Id\",,\"D [5];1-L-X-X-X-X\"";

        var result = AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(csv), "test.csv", catalog);
        var choice = Assert.Single(result.Choices);
        Assert.Single(choice.Selections);
        Assert.Contains(choice.Warnings, w => w.Contains("SEM [3]"));

        var missing = Assert.Single(result.MissingIdentities);
        Assert.Equal(3, missing.RowNumber);
        Assert.Equal("Ohne, Id", missing.DisplayName);
        Assert.Equal("KS1", missing.ClassName);
    }

    [Fact]
    public void RejectsFilesWithoutIdsWrongHeadersFormulasOrBrokenCodes()
    {
        var catalog = FullCatalog();

        var onlyMissingIds = ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\nKS1,\"Ohne, Id\",,\"D [5];1-L-X-X-X-X\"";
        Assert.Throws<InvalidDataException>(() => AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(onlyMissingIds), "no-id.csv", catalog));

        var wrongHeaders = "Name;Klasse;Jahr\nDemo;10a;2029";
        Assert.Throws<InvalidDataException>(() => AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(wrongHeaders), "wrong.csv", catalog));

        var brokenCode = ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\nKS1,\"Test, A\",demo-001,\"D [5];kaputt\"";
        Assert.Throws<InvalidDataException>(() => AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(brokenCode), "broken.csv", catalog));

        var duplicateOffer = ",Name mit Vornamen,ASV-ID,Fächer Kursstufe\nKS1,\"Test, A\",demo-001,\"D [5];1-L-X-X-X-X;D [5];2-L-X-X-X-X\"";
        Assert.Throws<InvalidDataException>(() => AsvCourseChoiceImport.Read(Encoding.UTF8.GetBytes(duplicateOffer), "dup.csv", catalog));

        var formulaStream = new MemoryStream();
        using (var zip = new ZipArchive(formulaStream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
            Add("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Export\" r:id=\"r1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id=\"r1\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add("xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>ASV-ID</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Fächer Kursstufe</t></is></c></row><row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>demo-1</t></is></c><c r=\"B2\"><f>\"D [5]\"</f><v>D</v></c></row></sheetData></worksheet>");
        }
        Assert.Throws<InvalidDataException>(() => AsvCourseChoiceImport.Read(formulaStream.ToArray(), "formula.xlsx", catalog));
    }

    [Fact]
    public void StudentColumnMatcherRecognizesAsvHeadersAndPrioritizesExactInternalKeys()
    {
        var asvHeaders = ImmutableArray.Create("Klasse", "Nachname", "Vornamen", "ASV-ID", "Religionsunterricht", "Fremdsprachen", "Abiturjahrgang");
        var matched = StudentColumnMatcher.Match(asvHeaders);
        Assert.Equal("ASV-ID", matched["studentId"]);
        Assert.Equal("Nachname", matched["lastName"]);
        Assert.Equal("Vornamen", matched["firstName"]);
        Assert.Equal("Klasse", matched["class"]);
        Assert.Equal("Religionsunterricht", matched["religion"]);
        Assert.Equal("Fremdsprachen", matched["languages"]);
        Assert.Equal("Abiturjahrgang", matched["year"]);

        var mixedHeaders = ImmutableArray.Create("studentId", "ASV-ID", "Nachname", "lastName");
        var mixed = StudentColumnMatcher.Match(mixedHeaders);
        Assert.Equal("studentId", mixed["studentId"]);
        Assert.Equal("lastName", mixed["lastName"]);
    }
}
