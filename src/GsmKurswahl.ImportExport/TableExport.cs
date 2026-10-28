using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public sealed record ExportTable(string[] Headers, string[][] Rows);
public static class TableExport
{
    public static ExportTable Choices(IEnumerable<TeacherState> states) => new(
        ["studentId", "year", "subject", "type", "variant", "semester", "class", "revision", "review", "departure"],
        [.. states.OrderBy(s => s.Record.Student.ExternalStudentId, StringComparer.Ordinal).Where(s => s.Active is not null)
            .SelectMany(s => s.Active!.Snapshot.Selections.OrderBy(c => c.OfferId, StringComparer.Ordinal)
            .SelectMany(c => c.Semesters.Order().Select(h => new[] { s.Record.Student.ExternalStudentId,
                s.Record.Student.GraduationYear.ToString(System.Globalization.CultureInfo.InvariantCulture), c.SubjectId, c.Type.ToString(), c.Variant.ToString(), h.ToString(System.Globalization.CultureInfo.InvariantCulture),
                s.Record.Student.ClassName, s.Active.Id.ToString(), Status(s.Validation?.Status), s.Record.Student.Departure.ToString() }))) ]);
    public static string Status(ReviewStatus? status) => status switch { ReviewStatus.Valid => "valid", ReviewStatus.Invalid => "invalid", _ => "needs_review" };
    public static byte[] Csv(ExportTable table)
    {
        // Quote every cell and neutralize spreadsheet formulas, including leading whitespace.
        string Cell(string value)
        {
            if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' || value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return Encoding.UTF8.GetBytes(string.Join("\r\n", new[] { table.Headers }.Concat(table.Rows).Select(r => string.Join(';', r.Select(Cell)))) + "\r\n");
    }
    public static byte[] Xlsx(ExportTable table)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Add(string path, XDocument doc) { using var stream = zip.CreateEntry(path).Open(); doc.Save(stream); }
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Add("[Content_Types].xml", new(new XElement(ct + "Types",
                new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Add("_rels/.rels", new(new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", rel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
            Add("xl/workbook.xml", new(new XElement(ns + "workbook", new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "Export"), new XAttribute("sheetId", "1"), new XAttribute(rel + "id", "rId1"))))));
            Add("xl/_rels/workbook.xml.rels", new(new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", rel.NamespaceName + "/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")))));
            string Column(int index) { var result = ""; for (var n = index + 1; n > 0; n = (n - 1) / 26) result = (char)('A' + (n - 1) % 26) + result; return result; }
            Add("xl/worksheets/sheet1.xml", new(new XElement(ns + "worksheet", new XElement(ns + "sheetData",
                new[] { table.Headers }.Concat(table.Rows).Select((row, r) => new XElement(ns + "row", new XAttribute("r", r + 1),
                    row.Select((value, c) => new XElement(ns + "c", new XAttribute("r", Column(c) + (r + 1)), new XAttribute("t", "inlineStr"),
                        new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))))))))));
        }
        return output.ToArray();
    }
}
