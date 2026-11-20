using System.IO.Compression;
using System.Xml.Linq;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public sealed record ReselectionStep(string Heading, string Text, string? Options = null);

public sealed record ReselectionPrintItem(StudentRecord Student, CourseChoice Remaining, string[] Removed)
{
    public string[] Notes { get; init; } = [];
    public ReselectionStep[] Steps { get; init; } = [];
    public string? SummaryNote { get; init; }
}

/// <summary>Editable offline OOXML: clones the retained GSM stationery and replaces only its content slots.</summary>
public static class ReselectionWord
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static XElement E(string name, params object[] content) => new(W + name, content);
    private static XAttribute A(string name, object value) => new(W + name, value);
    private static XElement P(string text, string? style = null, bool keep = false) => E("p",
        E("pPr", style is null ? Array.Empty<object>() : new object[] { E("pStyle", A("val", style)) },
            keep ? new object[] { E("keepNext") } : Array.Empty<object>(), E("spacing", A("after", "100"))), E("r", E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    private static XElement BulletStep(ReselectionStep step) => E("p",
        E("pPr", E("keepNext"), E("spacing", A("after", string.IsNullOrWhiteSpace(step.Options) ? "100" : "40"))),
        E("r", E("rPr", E("b")), E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), "• " + step.Heading + ": ")),
        E("r", E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), step.Text)));
    private static XElement StepOptions(string options) => E("p",
        E("pPr", E("ind", A("left", "240")), E("spacing", A("after", "100"))),
        E("r", E("rPr", E("sz", A("val", "18")), E("color", A("val", "505050"))), E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), options)));
    private static XElement Table(IEnumerable<string[]> rows)
    {
        int[] widths = [4200, 1750, 3120];
        var table = E("tbl", E("tblPr", E("tblW", A("w", "9070"), A("type", "dxa")), E("tblLayout", A("type", "fixed")),
            E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(n => E(n, A("val", "single"), A("sz", "4"), A("color", "AAAAAA"))).ToArray()),
            E("tblCellMar", E("top", A("w", "55"), A("type", "dxa")), E("bottom", A("w", "55"), A("type", "dxa")))),
            E("tblGrid", widths.Select(v => E("gridCol", A("w", v))).ToArray()));
        var index = 0;
        foreach (var row in rows)
        {
            var header = index++ == 0;
            table.Add(E("tr", E("trPr", E("cantSplit"), header ? new object[] { E("tblHeader") } : Array.Empty<object>()),
                row.Select((value, i) => E("tc", E("tcPr", E("tcW", A("w", widths[i]), A("type", "dxa")),
                    header ? new object[] { E("shd", A("fill", "F0F0F0")) } : Array.Empty<object>()),
                    E("p", E("pPr", E("spacing", A("after", "0"))), E("r", header ? E("rPr", E("b")) : E("rPr"), E("t", value))))).ToArray()));
        }
        return table;
    }
    public static byte[] Render(IEnumerable<ReselectionPrintItem> items, OfferCatalog catalog)
    {
        var ordered = items.OrderBy(x => x.Student.Student.ClassName, StringComparer.CurrentCulture)
            .ThenBy(x => x.Student.Student.SortName().Last, StringComparer.CurrentCulture)
            .ThenBy(x => x.Student.Student.SortName().First, StringComparer.CurrentCulture)
            .ThenBy(x => x.Remaining.StudentId, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("Keine Nachwahlbögen vorhanden.");
        using var template = typeof(ReselectionWord).Assembly.GetManifestResourceStream("GsmKurswahl.ImportExport.Assets.SchoolTemplate.docx")!;
        using var source = new ZipArchive(template, ZipArchiveMode.Read);
        var parts = source.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray(); });
        XDocument Read(string name) => XDocument.Load(new MemoryStream(parts[name]));
        var document = Read("word/document.xml");
        var body = document.Root!.Element(W + "body")!;
        var headerTable = new XElement(body.Elements(W + "tbl").First());
        var titleTable = new XElement(body.Elements(W + "tbl").Last());
        var geometry = new XElement(body.Element(W + "sectPr")!);
        body.RemoveNodes();
        var relationships = Read("word/_rels/document.xml.rels");
        var contentTypes = Read("[Content_Types].xml");
        XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        void Put(string name, XDocument xml) { using var m = new MemoryStream(); xml.Save(m); parts[name] = m.ToArray(); }
        void ContentType(string name, string type) => contentTypes.Root!.Add(new XElement(ct + "Override", new XAttribute("PartName", "/word/" + name), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml." + type + "+xml")));
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            var heading = new XElement(headerTable);
            var texts = heading.Descendants(W + "t").ToArray();
            texts[0].Value = "Kurswahl";
            texts[1].Value = "Oberstufenberatung - " + DateTime.Now.ToString("dd.MM.yyyy");
            var box = new XElement(titleTable); box.Descendants(W + "t").Single().Value = "Nachwahl zur Kurswahl";
            var headerName = "gsm-header-" + index + ".xml";
            var headerId = "gsmHeader" + index;
            var header = E("hdr", new XAttribute(XNamespace.Xmlns + "r", R), heading,
                E("p", E("pPr", E("spacing", A("after", "0"), A("line", "520"), A("lineRule", "exact")))), box,
                P($"Abitur {catalog.GraduationYear} · {item.Remaining.StudentId}", keep: true));
            Put("word/" + headerName, new XDocument(header));
            // The source drawing keeps its exact original relationship ID and image dimensions.
            Put("word/_rels/" + headerName + ".rels", new XDocument(new XElement(Rel + "Relationships",
                new XElement(Rel + "Relationship", new XAttribute("Id", "rId11"), new XAttribute("Type", R.NamespaceName + "/image"), new XAttribute("Target", "media/image1.jpeg")))));
            relationships.Root!.Add(new XElement(Rel + "Relationship", new XAttribute("Id", headerId), new XAttribute("Type", R.NamespaceName + "/header"), new XAttribute("Target", headerName)));
            ContentType(headerName, "header");
            body.Add(P(item.Student.Student.DisplayName + " · Klasse " + item.Student.Student.ClassName));
            if (item.Removed.Length > 0)
            {
                body.Add(P("Warum eine Nachwahl nötig ist", "GsmHeading", keep: true));
                body.Add(P(item.Removed.Length == 1
                    ? "Folgender gewählter Kurs kann leider nicht angeboten werden:"
                    : "Folgende gewählte Kurse können leider nicht angeboten werden:", keep: true));
                foreach (var id in item.Removed)
                {
                    var offer = catalog.Offers.Single(o => o.Id == id);
                    body.Add(P("• " + PlainDocumentText.FormatOfferFull(offer, catalog), keep: true));
                }
            }
            else
            {
                body.Add(P("Warum eine Nachwahl oder Korrektur nötig ist", "GsmHeading", keep: true));
                body.Add(P("Deine bisherige Kurswahl ist noch nicht vollständig oder enthält noch offene Punkte, die angepasst werden müssen.", keep: true));
            }
            if (item.Steps.Length > 0)
            {
                body.Add(P(item.Removed.Length > 0 ? "Was du jetzt neu wählen musst" : "Was du jetzt ergänzen oder korrigieren musst", "GsmHeading", keep: true));
                foreach (var step in item.Steps)
                {
                    body.Add(BulletStep(step));
                    if (!string.IsNullOrWhiteSpace(step.Options))
                        body.Add(StepOptions(step.Options));
                }
            }
            else if (item.Notes.Length > 0)
            {
                body.Add(P(item.Removed.Length > 0 ? "Was du jetzt neu wählen musst" : "Was du jetzt ergänzen oder korrigieren musst", "GsmHeading", keep: true));
                foreach (var note in item.Notes)
                    body.Add(P("• " + PlainDocumentText.Sanitize(note, catalog)));
            }
            else if (!string.IsNullOrWhiteSpace(item.SummaryNote))
            {
                body.Add(P("Hinweis zu deiner verbleibenden Wahl", "GsmHeading", keep: true));
                body.Add(P(item.SummaryNote));
            }
            body.Add(P("Bitte trage deine Nachwahl (und ggf. weitere Umwahlen) unten ein und gib diesen Bogen unterschrieben bei der Oberstufenberatung ab. Die neue Wahl wird anschließend geprüft."));
            body.Add(P("Deine verbleibenden Kurse", "GsmHeading"));
            var rows = new List<string[]> { new[] { "Fach und Hinweise", "Kursart", "Belegung" } };
            rows.AddRange(item.Remaining.Selections
                .OrderBy(s => s.Type)
                .ThenBy(s => PlainDocumentText.SubjectName(catalog, s.SubjectId), StringComparer.CurrentCulture)
                .Select(s => new[]
                {
                    PlainDocumentText.SubjectName(catalog, s.SubjectId) + (s.Variant == CourseVariant.Bilingual ? " bilingual" : "") + CourseAnnotations.Suffix(catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)),
                    PlainDocumentText.FormatCourseTypeFull(s.Type),
                    PlainDocumentText.FormatSemesters(s.Semesters)
                }));
            body.Add(Table(rows));
            body.Add(P("Bisher geplante mündliche Prüfungsfächer (unverbindlich): " + (item.Remaining.PlannedOralSubjects.IsEmpty
                ? "noch nicht geplant"
                : string.Join(", ", item.Remaining.PlannedOralSubjects.Select(o => PlainDocumentText.SubjectName(catalog, o))))));
            body.Add(P("Nachwahl und weitere Umwahlen", "GsmHeading"));
            body.Add(P("Neu gewähltes Fach, Kursart (Leistungsfach/Basisfach/Wahlfach) und Halbjahre; bei einer Umwahl bitte auch das bisherige Fach angeben.", keep: true));
            for (var i = 0; i < 3; i++) body.Add(E("p", E("pPr", E("keepNext"), E("spacing", A("before", "160"), A("after", "100")),
                E("pBdr", new[] { "bottom", "between" }.Select(n => E(n, A("val", "single"), A("sz", "4"), A("color", "AAAAAA"))).ToArray())), E("r", E("t", " "))));
            body.Add(P("Die endgültige Belegbarkeit wird durch die Oberstufenberatung geprüft.", keep: true));
            body.Add(P(" ", keep: true));
            body.Add(P("________________________        ____________________________________", keep: true));
            body.Add(P("Ort, Datum                                      Unterschrift Schülerin / Schüler"));
            var section = new XElement(geometry);
            section.Elements(W + "headerReference").Remove(); section.Elements(W + "footerReference").Remove();
            section.AddFirst(E("headerReference", A("type", "default"), new XAttribute(R + "id", headerId)),
                E("footerReference", A("type", "default"), new XAttribute(R + "id", "rId12")));
            var margin = section.Element(W + "pgMar")!;
            margin.SetAttributeValue(W + "top", "3900"); margin.SetAttributeValue(W + "header", "1134");
            if (index < ordered.Length - 1) body.Add(E("p", E("pPr", section)));
            else body.Add(section);
        }
        var footer = E("ftr", E("p", E("pPr", E("jc", A("val", "right"))),
            E("r", E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), "Seite ")), E("fldSimple", A("instr", "PAGE"), E("r", E("t", "1")))));
        Put("word/footer1.xml", new XDocument(footer));
        var styles = Read("word/styles.xml");
        var defaults = styles.Root!.Element(W + "docDefaults")!.Element(W + "rPrDefault")!.Element(W + "rPr")!;
        defaults.ReplaceNodes(E("rFonts", A("ascii", "Arial"), A("hAnsi", "Arial")), E("sz", A("val", "20")), E("color", A("val", "000000")), E("lang", A("val", "de-DE")));
        styles.Root!.Add(E("style", A("type", "paragraph"), A("styleId", "GsmHeading"), E("name", A("val", "GSM Abschnitt")),
            E("pPr", E("keepNext"), E("spacing", A("before", "140"), A("after", "100"))), E("rPr", E("b"), E("sz", A("val", "22")), E("color", A("val", "000000")))));
        var settings = Read("word/settings.xml"); settings.Root!.Element(W + "updateFields")?.Remove(); settings.Root!.Add(E("updateFields", A("val", "true")));
        Put("word/settings.xml", settings); Put("word/styles.xml", styles); Put("word/document.xml", document);
        Put("word/_rels/document.xml.rels", relationships); Put("[Content_Types].xml", contentTypes);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var (name, data) in parts) { using var stream = zip.CreateEntry(name).Open(); stream.Write(data); }
        return output.ToArray();
    }
}
