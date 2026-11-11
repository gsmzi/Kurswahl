using System.IO.Compression;
using System.Xml.Linq;
using GsmKurswahl.Domain;
using GsmKurswahl.Tests;

namespace GsmKurswahl.ImportExport.Tests;

public sealed class SchoolStationeryTests
{
    [Fact]
    public void EveryPrintKindUsesSchoolStationeryAndCourseAnnotations()
    {
        var catalog = TestData.WorkflowCatalog with { Subjects = [.. TestData.WorkflowCatalog.Subjects.Select(s => s with { Name = s.Id switch
        { "D" => "Deutsch", "M" => "Mathematik", "E" => "Englisch", "G" => "Geschichte", "BIO" => "Biologie", "CH" => "Chemie", "SP" => "Sport", "BK" => "Bildende Kunst", "MU" => "Musik", "GK" => "Gemeinschaftskunde", "GEO" => "Geographie", "ETH" => "Ethik", _ => s.Name } })] };
        catalog = catalog with { Offers = [.. catalog.Offers.Select(o => o.Id switch
        {
            "E-LF" => o with { CooperationSchool = CooperationSchool.Ewg },
            "CH-BF" => o with { CooperationSchool = CooperationSchool.Gsm, IsTopUpCourse = true },
            _ => o
        })] };
        var choice = TestData.Choice with { Selections = [.. catalog.Offers.Where(o => o.Id is "D-LF" or "M-LF" or "E-LF" || o.Type == CourseType.Bf && o.SubjectId is not ("D" or "M" or "E" or "MU")).Select(TestData.Select)], PlannedOralSubjects = ["BIO", "G"] };
        var student = new StudentRecord(SchemaVersions.StudentRecord, new(Guid.NewGuid(), "demo-001", "Muster, Demo", "10a", 2029, DepartureStatus.Active, PriorEducation.Unknown), "", "", "", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null);
        var item = new ReselectionPrintItem(student, choice, ["E-LF"]) { Notes = ["Bitte ein neues Leistungsfach wählen."] };
        var notesOnly = item with { Removed = [], Notes = ["Die Pflichtbelegung in Geschichte muss ergänzt werden."] };
        var outputs = new Dictionary<string, byte[]>
        {
            ["Kontrollbogen-Schueler.pdf"] = ControlSheet.Render(choice, catalog,
                new(ProfileStatus.Verified, PriorEducation.Unknown) { DisplayName = "Demo Muster", ClassName = "10a" },
                new(ReviewStatus.NeedsReview, []), [new(1, 12, 34), new(2, 12, 34), new(3, 11, 32), new(4, 11, 32)], [new("BIO", "G", 36, ReviewStatus.NeedsReview, "Test")]),
            ["Kontrollbogen-Lehrer.pdf"] = PlanningPrint.ControlSheets([new(student, choice, [], ["Die persönliche Berechtigung für einzelne Fächer ist noch zu klären."])], catalog),
            ["Nachwahl.pdf"] = PlanningPrint.Reselections([item, notesOnly with { Student = student with { Student = student.Student with { SyntheticDisplayName = "Zweiter, Demo" } }, Remaining = choice with { StudentId = "demo-002" } }], catalog),
            ["Kursliste.pdf"] = PlanningPrint.CourseLists(2029, [new("Englisch LF · Koopkurs am EWG", Enumerable.Range(1, 42).Select(i => new[] { i.ToString(), $"Testperson {i:000}, Demo", "10a", "HJ 1-4" }).ToArray())]),
            ["Wahlzahlen.pdf"] = PlanningPrint.Demand(2029, "Aktueller Jahrgang", "Ohne bestätigte Abgänger", [["Englisch", "24", "-", "50", "-", "-"], ["Chemie", "14", "-", "19", "-", "-"]], catalog),
            ["Uebersicht.pdf"] = PlanningPrint.Overview(2029, "Alle Personen", Enumerable.Range(1, 25).Select(i => new[] { $"Testperson {i:000}, Demo", "10a", "D, M, E [Koopkurs am EWG]", "BIO, CH [Koopkurs am GSM · Aufsetzerkurs], G, GK, GEO, SP, BK, ETH", "BIO, G", "Fachlich zu klären" }).ToArray()),
            ["Nachwahl.docx"] = ReselectionWord.Render([item, notesOnly], catalog),
            ["Nachwahl-lang.docx"] = ReselectionWord.Render([notesOnly with { Notes = Enumerable.Range(1, 18).Select(i => $"Prüfhinweis {i}: Offene Belegung mit der Oberstufenberatung klären; bitte die betreffenden Halbjahre auf dem Bogen angeben.").ToArray() }, item], catalog)
        };
        using var zip = new ZipArchive(new MemoryStream(outputs["Nachwahl.docx"]));
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using var main = zip.GetEntry("word/document.xml")!.Open();
        var xml = XDocument.Load(main);
        Assert.Equal(2, xml.Descendants(w + "sectPr").Count());
        Assert.Contains("Die Pflichtbelegung in Geschichte muss ergänzt werden.", xml.ToString());
        Assert.Contains("Koopkurs am GSM", xml.ToString()); Assert.Contains("Aufsetzerkurs", xml.ToString());
        Assert.Single(xml.Descendants(w + "t"), t => t.Value.Contains("Folgende gewählte Kurse"));
        Assert.Equal(2, zip.Entries.Count(e => e.FullName.StartsWith("word/gsm-header-")));
        Assert.NotNull(zip.GetEntry("word/media/image1.jpeg"));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("docProps/") || e.FullName.StartsWith("customXml/"));
        Assert.All(xml.Descendants(w + "pgMar"), m => Assert.Equal("1418", m.Attribute(w + "left")!.Value));
        if (Environment.GetEnvironmentVariable("GSM_PRINT_REVIEW") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var (name, bytes) in outputs) File.WriteAllBytes(Path.Combine(directory, name), bytes);
        }
    }
}
