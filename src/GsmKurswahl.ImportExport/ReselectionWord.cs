using System.IO.Compression;
using System.Xml.Linq;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public sealed record ReselectionPrintItem(StudentRecord Student, CourseChoice Remaining, string[] Removed);

/// <summary>Minimal editable OOXML document, produced offline without Office automation.</summary>
public static class ReselectionWord
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static XElement E(string name, params object[] content) => new(W + name, content);
    private static XAttribute A(string name, object value) => new(W + name, value);
    private static XElement P(string text, string? style = null, bool pageBreak = false) => E("p",
        E("pPr", E("spacing", A("after", "100")), style is null ? Array.Empty<object>() : new object[] { E("pStyle", A("val", style)) },
            pageBreak ? new object[] { E("pageBreakBefore") } : Array.Empty<object>()), E("r", E("t", text)));
    private static XElement Table(IEnumerable<string[]> rows)
    {
        var table = E("tbl", E("tblPr", E("tblW", A("w", "10200"), A("type", "dxa")), E("tblLayout", A("type", "fixed")),
            E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(n => E(n, A("val", "single"), A("sz", "4"), A("color", "CBD5DC"))).ToArray()),
            E("tblCellMar", E("top", A("w", "55"), A("type", "dxa")), E("bottom", A("w", "55"), A("type", "dxa")))),
            E("tblGrid", new[] { 5000, 1400, 3800 }.Select(v => E("gridCol", A("w", v))).ToArray()));
        var index = 0;
        foreach (var row in rows)
        {
            var header = index++ == 0;
            table.Add(E("tr", E("trPr", E("cantSplit"), header ? new object[] { E("tblHeader") } : Array.Empty<object>()),
                row.Select((value, i) => E("tc", E("tcPr", E("tcW", A("w", new[] { 5000, 1400, 3800 }[i]), A("type", "dxa")),
                    header ? new object[] { E("shd", A("fill", "E9F4F3")) } : Array.Empty<object>()),
                    E("p", E("pPr", E("spacing", A("after", "0"))), E("r", header ? E("rPr", E("b")) : E("rPr"), E("t", value))))).ToArray()));
        }
        return table;
    }
    public static byte[] Render(IEnumerable<ReselectionPrintItem> items, OfferCatalog catalog)
    {
        var ordered = items.OrderBy(x => x.Student.Student.ClassName).ThenBy(x => x.Student.Student.SyntheticDisplayName).ThenBy(x => x.Remaining.StudentId).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("Keine Nachwahlbögen vorhanden.");
        var body = E("body");
        foreach (var item in ordered)
        {
            body.Add(P("Nachwahl zur Kurswahl", "Title", item != ordered[0]));
            body.Add(P($"Abitur {catalog.GraduationYear} | {item.Student.Student.SyntheticDisplayName} | Klasse {item.Student.Student.ClassName}"));
            body.Add(P("Folgende gewählte Kurse können leider nicht angeboten werden:"));
            foreach (var id in item.Removed)
            {
                var offer = catalog.Offers.Single(o => o.Id == id);
                var type = offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminarkurs" };
                body.Add(P(catalog.Subjects.Single(s => s.Id == offer.SubjectId).Name + " - " + type + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : "")));
            }
            body.Add(P("Deshalb ist eine Nachwahl bzw. Rückmeldung erforderlich. Bitte trage deine Nachwahl und gegebenenfalls weitere Umwahlen unten ein und gib diesen Bogen unterschrieben bei der Oberstufenberatung ab. Die neue Wahl wird anschließend geprüft."));
            body.Add(P("Deine verbleibenden Kurse", "Heading1"));
            var rows = new List<string[]> { new[] { "Fach / Variante", "Kursart", "Belegung" } };
            rows.AddRange(item.Remaining.Selections.OrderBy(s => s.Type).ThenBy(s => s.SubjectId).Select(s => new[] {
                catalog.Subjects.Single(x => x.Id == s.SubjectId).Name + (s.Variant == CourseVariant.Bilingual ? " bilingual" : ""),
                s.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminar" },
                string.Join(", ", s.Semesters.Select(h => "HJ " + h)) }));
            body.Add(Table(rows));
            body.Add(P("Nachwahl und weitere Umwahlen", "Heading1"));
            body.Add(P("Neu gewähltes Fach / Kursart / Halbjahre; bei Umwahl auch das bisherige Fach nennen."));
            for (var i = 0; i < 3; i++) body.Add(E("p", E("pPr", E("spacing", A("before", "160"), A("after", "100")), E("pBdr", E("bottom", A("val", "single"), A("sz", "4"), A("color", "CBD5DC")))), E("r", E("t", " "))));
            body.Add(P("Die endgültige Belegbarkeit wird durch die Oberstufenberatung geprüft."));
            body.Add(P(" "));
            body.Add(P("________________________                  ____________________________________"));
            body.Add(P("Ort, Datum                                              Unterschrift Schülerin / Schüler"));
        }
        body.Add(E("sectPr", E("pgSz", A("w", "11906"), A("h", "16838")), E("pgMar", A("top", "800"), A("bottom", "800"), A("left", "850"), A("right", "850"))));
        var styles = E("styles", E("docDefaults", E("rPrDefault", E("rPr", E("rFonts", A("ascii", "Arial"), A("hAnsi", "Arial")), E("sz", A("val", "20")), E("color", A("val", "000000"))))));
        foreach (var (id, size) in new[] { ("Title", "36"), ("Heading1", "25") })
            styles.Add(E("style", A("type", "paragraph"), A("styleId", id), E("name", A("val", id)), E("pPr", E("keepNext"), E("spacing", A("before", "140"), A("after", "120"))), E("rPr", E("b"), E("sz", A("val", size)), E("color", A("val", "000000")))));
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Part(string path, XElement xml) { using var stream = zip.CreateEntry(path).Open(); new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(stream); }
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            Part("[Content_Types].xml", new(ct + "Types", new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/word/document.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/word/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"))));
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            Part("_rels/.rels", new(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "word/document.xml"))));
            Part("word/_rels/document.xml.rels", new(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), new XAttribute("Target", "styles.xml"))));
            Part("word/document.xml", E("document", body)); Part("word/styles.xml", styles);
        }
        return output.ToArray();
    }
}
