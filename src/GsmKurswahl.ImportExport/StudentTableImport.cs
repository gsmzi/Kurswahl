using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public sealed record ImportTable(ImmutableArray<string> Headers, ImmutableArray<ImmutableArray<string>> Rows);
public sealed record StudentImportRow(int Row, StudentRecord? Candidate, string Error);
public static class StudentTableImport
{
    public static readonly string[] Fields = ["studentId", "name", "firstName", "lastName", "folder", "class", "year", "username", "accountId", "home", "italian", "bridge", "imp", "bilingual", "religion", "sport", "languages", "religionPermission", "religionExamPermission", "permanentSport", "departure"];
    public static ImportTable Csv(string text, char separator = ';')
    {
        if (text.Length > 8_000_000) throw new InvalidDataException("Importdatei zu groß.");
        var rows = new List<ImmutableArray<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false; var closedQuote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (closedQuote && c != separator && c is not ('\r' or '\n')) throw new InvalidDataException("Unerwarteter Text nach CSV-Anführungszeichen.");
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (quoted) { quoted = false; closedQuote = true; }
                else if (cell.Length == 0) quoted = true;
                else throw new InvalidDataException("Anführungszeichen innerhalb unquotierten CSV-Felds.");
            }
            else if (!quoted && c == separator) { row.Add(cell.ToString()); cell.Clear(); closedQuote = false; }
            else if (!quoted && c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); closedQuote = false; if (row.Any(v => v.Length > 0)) rows.Add([.. row]); row.Clear();
            }
            else cell.Append(c);
        }
        if (quoted) throw new InvalidDataException("Nicht geschlossenes CSV-Anführungszeichen.");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return Table(rows);
    }
    public static ImportTable Xlsx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Sum(e => e.Length) > 16_000_000) throw new InvalidDataException("XLSX zu groß.");
        XDocument Read(string name)
        {
            using var input = (zip.GetEntry(name) ?? throw new InvalidDataException("XLSX-Bestandteil fehlt: " + name)).Open();
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16_000_000 });
            return XDocument.Load(reader);
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheets = Read("xl/workbook.xml").Descendants(ns + "sheet").ToArray();
        if (sheets.Length != 1) throw new InvalidDataException("Bitte genau ein Tabellenblatt für den Import bereitstellen.");
        var relation = Read("xl/_rels/workbook.xml.rels").Root!.Elements().Single(e => (string?)e.Attribute("Id") == (string?)sheets[0].Attribute(rel + "id"));
        var target = (string?)relation.Attribute("Target") ?? throw new InvalidDataException("Tabellenblatt fehlt.");
        if (target.Contains("..", StringComparison.Ordinal) || (string?)relation.Attribute("TargetMode") == "External") throw new InvalidDataException("Externe XLSX-Verknüpfung abgewiesen.");
        var normalizedTarget = target.TrimStart('/');
        if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) normalizedTarget = "xl/" + normalizedTarget;
        var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Read("xl/sharedStrings.xml").Descendants(ns + "si").Select(e => string.Concat(e.Descendants(ns + "t").Select(t => t.Value))).ToArray();
        var rows = new List<ImmutableArray<string>>();
        foreach (var r in Read(normalizedTarget).Descendants(ns + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var c in r.Elements(ns + "c"))
            {
                if (c.Element(ns + "f") is not null) throw new InvalidDataException("Formelzellen im Import nicht erlaubt; Werte exportieren.");
                var reference = (string?)c.Attribute("r") ?? throw new InvalidDataException("Zelladresse fehlt.");
                var index = 0; foreach (var letter in reference.TakeWhile(char.IsLetter)) index = checked(index * 26 + char.ToUpperInvariant(letter) - 'A' + 1);
                if (index is < 1 or > 100) throw new InvalidDataException("Zu viele Spalten.");
                var value = c.Element(ns + "v")?.Value ?? string.Concat(c.Descendants(ns + "t").Select(t => t.Value));
                var text = value;
                if ((string?)c.Attribute("t") == "s")
                {
                    if (!int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var sharedIndex) || sharedIndex < 0 || sharedIndex >= shared.Length)
                        throw new InvalidDataException("Ungültiger XLSX-Textverweis.");
                    text = shared[sharedIndex];
                }
                cells[index - 1] = text;
            }
            if (cells.Count > 0) rows.Add([.. Enumerable.Range(0, cells.Keys.Max() + 1).Select(i => cells.GetValueOrDefault(i, ""))]);
        }
        return Table(rows);
    }
    private static ImportTable Table(List<ImmutableArray<string>> rows)
    {
        if (rows.Count < 2 || rows.Count > 10001) throw new InvalidDataException("Import benötigt Kopfzeile und 1–10000 Datenzeilen.");
        var headers = rows[0].Select(h => h.Trim().TrimStart('\uFEFF')).ToImmutableArray();
        if (headers.Length > 100) throw new InvalidDataException("Zu viele Spalten.");
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length) throw new InvalidDataException("Leere/doppelte Spaltenüberschrift.");
        return new(headers, [.. rows.Skip(1)]);
    }
    public static ImmutableArray<StudentImportRow> Preview(ImportTable table, IReadOnlyDictionary<string, string> mapping, ImportOrigin origin, string actor)
    {
        bool HasMapped(string field) => mapping.TryGetValue(field, out var header) && table.Headers.Contains(header);
        foreach (var field in new[] { "studentId", "class", "year" })
            if (!HasMapped(field)) throw new ArgumentException("Spaltenzuordnung fehlt: " + field);
        if (!HasMapped("name") && !(HasMapped("firstName") && HasMapped("lastName")))
            throw new ArgumentException("Spaltenzuordnung fehlt: name oder firstName + lastName.");
        if (mapping.Keys.Any(k => !Fields.Contains(k)) || mapping.Values.Distinct().Count() != mapping.Count) throw new ArgumentException("Unbekannte oder mehrfach zugeordnete Spalte.");
        var result = new List<StudentImportRow>();
        for (var i = 0; i < table.Rows.Length; i++)
        {
            try
            {
                var row = table.Rows[i];
                if (row.Length > table.Headers.Length) throw new ArgumentException("Mehr Werte als Spalten.");
                string Value(string field) => mapping.TryGetValue(field, out var header) && table.Headers.IndexOf(header) is var col && col >= 0 && col < row.Length ? row[col].Trim() : "";
                Knowledge Known(string field) => Value(field).ToLowerInvariant() switch { "" or "unknown" or "unbekannt" => Knowledge.Unknown, "ja" or "yes" or "true" or "1" => Knowledge.Yes, "nein" or "no" or "false" or "0" => Knowledge.No, _ => throw new ArgumentException("Ungültiger Wahrheitswert: " + field) };
                var id = Value("studentId");
                if (!id.StartsWith("demo-", StringComparison.Ordinal)) throw new ArgumentException("Nur synthetische demo-IDs erlaubt.");
                var name = Value("name");
                if (name.Length == 0 && Value("firstName").Length > 0 && Value("lastName").Length > 0) name = Value("firstName") + " " + Value("lastName");
                var home = Value("home");
                if (home.Length == 0)
                {
                    var folder = Value("folder");
                    if (folder.Length == 0 && Value("firstName").Length > 0 && Value("lastName").Length > 0) folder = Value("lastName") + "." + Value("firstName");
                    if (folder.Length > 0)
                    {
                        bool InvalidPart(string part) => string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || part.EndsWith('.') || part.EndsWith(' ');
                        if (InvalidPart(Value("class")) || InvalidPart(folder)) throw new ArgumentException("Klasse/Schülerordner muss ein einzelner gültiger Ordnername sein.");
                        home = Value("class") + "/" + folder;
                    }
                }
                var year = int.Parse(Value("year"), System.Globalization.CultureInfo.InvariantCulture);
                if (year is < 2000 or > 2200 || name.Length == 0) throw new ArgumentException("Name oder Jahrgang ungültig.");
                var sport = Value("sport").ToLowerInvariant();
                ImmutableArray<int>? semesters = sport is "" or "unknown" or "unbekannt" ? null : sport is "keine" or "none" ? [] : [.. sport.Split(',', StringSplitOptions.TrimEntries).Select(int.Parse).Order()];
                if (semesters is { } h && (h.Any(v => v is < 1 or > 4) || h.Distinct().Count() != h.Length)) throw new ArgumentException("Sport-Halbjahre ungültig.");
                var religion = Value("religion").ToUpperInvariant();
                if (religion is not ("" or "ETH" or "ER" or "KR")) throw new ArgumentException("Religion/Ethik ungültig.");
                var prior = new PriorEducation(Known("italian"), Known("bridge"), Known("imp"), Known("bilingual"), religion.Length == 0 ? null : religion, semesters)
                { ContinuedLanguages = [.. Value("languages").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()], ReligionEthicsPermission = Known("religionPermission"), ReligionEthicsExamPermission = Known("religionExamPermission"), PermanentSportExemption = Known("permanentSport") };
                var departure = Value("departure").ToLowerInvariant() switch { "" or "aktiv" => DepartureStatus.Active, "voraussichtlich" => DepartureStatus.ExpectedDeparture, "bestaetigt" or "bestätigt" => DepartureStatus.ConfirmedDeparture, _ => throw new ArgumentException("Abgängerstatus ungültig.") };
                var student = new AdministrativeStudent(Guid.NewGuid(), id, name, Value("class"), year, departure, prior);
                result.Add(new(i + 2, new(1, student, Value("username"), Value("accountId"), home, false, 1, actor, DateTimeOffset.UtcNow, origin), ""));
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { result.Add(new(i + 2, null, ex.Message)); }
        }
        var duplicates = result.Where(r => r.Candidate is not null).GroupBy(r => r.Candidate!.Student.ExternalStudentId, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accounts = result.Where(r => !string.IsNullOrWhiteSpace(r.Candidate?.WindowsUsername)).GroupBy(r => r.Candidate!.WindowsUsername, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sids = result.Where(r => !string.IsNullOrWhiteSpace(r.Candidate?.AccountId)).GroupBy(r => r.Candidate!.AccountId, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var homes = result.Where(r => !string.IsNullOrWhiteSpace(r.Candidate?.HomeDirectory)).GroupBy(r => r.Candidate!.HomeDirectory.Replace('\\', '/').TrimEnd('/'), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. result.Select(r => r.Candidate is { } c && (duplicates.Contains(c.Student.ExternalStudentId) || accounts.Contains(c.WindowsUsername) || sids.Contains(c.AccountId) || homes.Contains(c.HomeDirectory.Replace('\\', '/').TrimEnd('/'))) ? r with { Error = "Doppelte Schüler-ID, Benutzerkennung oder Ordner; keine Übernahme." } : r)];
    }
}
