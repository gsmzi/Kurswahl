using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public sealed record AsvMissingIdentityRow(int RowNumber, string DisplayName, string ClassName);
public sealed record AsvCourseImportResult(ImmutableArray<ExternalChoice> Choices, ImmutableArray<AsvMissingIdentityRow> MissingIdentities);

public static partial class AsvCourseChoiceImport
{
    private static readonly IReadOnlyDictionary<string, string> SubjectMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MUS"] = "MU",
        ["WI"] = "W",
        ["REV"] = "ER",
        ["RRK"] = "KR",
        ["SPO"] = "SP",
        ["PSYCH"] = "PSY"
    };

    [GeneratedRegex(@"^(.+?)\s*\[(\d+)\]$")]
    private static partial Regex SubjectTokenRegex();

    public static AsvCourseImportResult Read(byte[] bytes, string fileName, OfferCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(catalog);
        if (bytes.Length > 16_000_000) throw new InvalidDataException("Importdatei überschreitet 16 MB.");

        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
            throw new InvalidDataException("Bitte den ASV-BW-Export „zz Kursstufe Fächerwahl“ als .xlsx oder .csv auswählen.");

        List<ImmutableArray<string>> rows;
        if (extension == ".xlsx")
        {
            using var stream = new MemoryStream(bytes);
            rows = StudentTableImport.ReadXlsxRows(stream);
        }
        else
        {
            var text = DecodeCsvText(bytes);
            var separator = DetectSeparator(text);
            rows = StudentTableImport.ReadCsvRows(text, separator);
        }

        if (rows.Count == 0)
            throw new InvalidDataException("Die ASV-Datei ist leer.");
        if (rows.Count > 10001)
            throw new InvalidDataException("ASV-Datei enthält mehr als 10000 Datenzeilen.");

        var headers = rows[0].Select(h => h.Trim().TrimStart('\uFEFF')).ToImmutableArray();
        if (headers.Length > 100)
            throw new InvalidDataException("Zu viele Spalten.");
        var nonEmptyHeaders = headers.Where(h => h.Length > 0).ToArray();
        if (nonEmptyHeaders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nonEmptyHeaders.Length)
            throw new InvalidDataException("Doppelte Spaltenüberschrift in der ASV-Datei.");

        var idCol = FindColumn(headers, "ASV-ID");
        var coursesCol = FindColumn(headers, "Fächer Kursstufe");
        if (idCol < 0 || coursesCol < 0)
            throw new InvalidDataException("Keine ASV-Datei „zz Kursstufe Fächerwahl“ erkannt: Spalten „ASV-ID“ und „Fächer Kursstufe“ sind erforderlich.");

        if (rows.Count < 2)
            throw new InvalidDataException("Keine Schülerzeilen in der ASV-Datei vorhanden.");

        var nameCol = FindColumn(headers, "Name mit Vornamen");
        var classCol = FindColumn(headers, "Klasse");
        if (classCol < 0 && headers.Length > 0 && headers[0].Length == 0)
            classCol = 0;

        var choices = ImmutableArray.CreateBuilder<ExternalChoice>();
        var missingIdentities = ImmutableArray.CreateBuilder<AsvMissingIdentityRow>();
        var dataRowCount = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Length > headers.Length && row.Skip(headers.Length).Any(v => !string.IsNullOrWhiteSpace(v)))
                throw new InvalidDataException($"Zeile {rowNumber}: Mehr Werte als Spalten.");

            dataRowCount++;
            string Cell(int col) => col >= 0 && col < row.Length ? row[col].Trim() : "";

            var id = Cell(idCol);
            var name = Cell(nameCol);
            var className = Cell(classCol);
            if (id.Length == 0)
            {
                missingIdentities.Add(new(rowNumber, name, className));
                continue;
            }

            var coursesCell = Cell(coursesCol);
            var (selections, oral, warnings) = ParseCourses(coursesCell, rowNumber, catalog);
            choices.Add(new ExternalChoice(id, catalog.GraduationYear, selections)
            {
                PlannedOralSubjects = oral,
                CompareOralSubjects = true,
                IgnoreBilingualVariant = true,
                DisplayName = name,
                ClassName = className,
                Warnings = warnings
            });
        }

        if (dataRowCount == 0)
            throw new InvalidDataException("Keine Schülerzeilen in der ASV-Datei vorhanden.");
        if (choices.Count == 0 && missingIdentities.Count > 0)
            throw new InvalidDataException("Keine ASV-IDs in der Datei gefunden. Für den Abgleich muss die Spalte „ASV-ID“ gefüllt sein.");

        return new(choices.ToImmutable(), missingIdentities.ToImmutable());
    }

    private static (ImmutableArray<CourseSelection> Selections, ImmutableArray<string> Oral, ImmutableArray<string> Warnings)
        ParseCourses(string coursesCell, int rowNumber, OfferCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(coursesCell))
            return ([], [], []);

        var tokens = coursesCell.Split(';').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
        if (tokens.Length % 2 != 0)
            throw new InvalidDataException($"Zeile {rowNumber}: Unvollständiger Kurseintrag in „Fächer Kursstufe“.");

        var selections = new List<CourseSelection>();
        var seenOfferIds = new HashSet<string>(StringComparer.Ordinal);
        var oralSlots = new List<(int Order, string SubjectId)>();
        var warnings = new List<string>();

        for (var i = 0; i < tokens.Length; i += 2)
        {
            var subjectToken = tokens[i];
            var codeToken = tokens[i + 1];

            var match = SubjectTokenRegex().Match(subjectToken);
            if (!match.Success)
                throw new InvalidDataException($"Zeile {rowNumber}: Ungültiges Fachformat „{subjectToken}“ (erwartet z. B. „D [5]“).");

            var asvSubject = match.Groups[1].Value.Trim().ToUpperInvariant();
            if (!int.TryParse(match.Groups[2].Value, out _))
                throw new InvalidDataException($"Zeile {rowNumber}: Ungültige Stundenzahl in „{subjectToken}“.");

            var (prefix, flag, semesters) = ParseCodeToken(codeToken, rowNumber, subjectToken);
            if (semesters.IsEmpty) continue;

            var subjectId = SubjectMap.GetValueOrDefault(asvSubject, asvSubject);
            var courseType = flag.Equals("L", StringComparison.OrdinalIgnoreCase) || prefix is "1" or "2" or "3"
                ? CourseType.Lf
                : subjectId == "PSY"
                    ? CourseType.Elective
                    : subjectId == "SIA"
                        ? CourseType.Seminar
                        : CourseType.Bf;

            if (prefix.Equals("m1", StringComparison.OrdinalIgnoreCase))
                oralSlots.Add((1, subjectId));
            else if (prefix.Equals("m2", StringComparison.OrdinalIgnoreCase))
                oralSlots.Add((2, subjectId));

            var matching = catalog.Offers.Where(o => o.SubjectId == subjectId && o.Type == courseType).ToArray();
            var offer = matching.FirstOrDefault(o => o.Variant == CourseVariant.Normal) ?? matching.FirstOrDefault();
            if (offer is null)
            {
                warnings.Add($"Unbekanntes ASV-Fach im Katalog: {subjectToken} (HJ {string.Join(',', semesters)})");
                continue;
            }

            if (!seenOfferIds.Add(offer.Id))
                throw new InvalidDataException($"Zeile {rowNumber}: Doppelte Belegung für „{subjectToken}“.");

            selections.Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, semesters));
        }

        var oral = oralSlots
            .OrderBy(x => x.Order)
            .Select(x => x.SubjectId)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

        return ([.. selections.OrderBy(s => s.OfferId, StringComparer.Ordinal)], oral, [.. warnings]);
    }

    private static (string Prefix, string Flag, ImmutableArray<int> Semesters) ParseCodeToken(string codeToken, int rowNumber, string subjectToken)
    {
        if (codeToken.Length < 10 || codeToken[^8] != '-')
            throw new InvalidDataException($"Zeile {rowNumber}: Ungültiger Belegungscode „{codeToken}“ bei „{subjectToken}“.");

        var tail = codeToken[^7..];
        if (tail[1] != '-' || tail[3] != '-' || tail[5] != '-')
            throw new InvalidDataException($"Zeile {rowNumber}: Ungültiges Halbjahresmuster „{codeToken}“ bei „{subjectToken}“.");

        var head = codeToken[..^8];
        var headParts = head.Split('-');
        if (headParts.Length != 2 || string.IsNullOrWhiteSpace(headParts[0]) || string.IsNullOrWhiteSpace(headParts[1]))
            throw new InvalidDataException($"Zeile {rowNumber}: Ungültiges Kurskennzeichen „{codeToken}“ bei „{subjectToken}“.");

        var semesters = ImmutableArray.CreateBuilder<int>();
        for (var s = 0; s < 4; s++)
        {
            var mark = char.ToUpperInvariant(tail[s * 2]);
            if (mark is 'X' or 'Z') semesters.Add(s + 1);
            else if (mark != '-')
                throw new InvalidDataException($"Zeile {rowNumber}: Ungültiges Halbjahreszeichen „{tail[s * 2]}“ in „{codeToken}“.");
        }

        return (headParts[0].Trim(), headParts[1].Trim(), semesters.ToImmutable());
    }

    private static int FindColumn(ImmutableArray<string> headers, string name)
    {
        for (var i = 0; i < headers.Length; i++)
            if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static string DecodeCsvText(byte[] bytes)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = Encoding.Latin1.GetString(bytes);
        }
        return text.TrimStart('\uFEFF');
    }

    private static char DetectSeparator(string text)
    {
        var firstLine = FirstNonEmptyLine(text);
        if (firstLine.Length == 0) return ',';

        foreach (var candidate in new[] { ',', ';', '\t' })
        {
            var cells = SplitLine(firstLine, candidate).Select(c => c.Trim().TrimStart('\uFEFF')).ToArray();
            if (cells.Any(c => string.Equals(c, "ASV-ID", StringComparison.OrdinalIgnoreCase))
                && cells.Any(c => string.Equals(c, "Fächer Kursstufe", StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        var counts = new[] { ',', ';', '\t' }
            .Select(sep => (Sep: sep, Count: SplitLine(firstLine, sep).Count))
            .OrderByDescending(x => x.Count)
            .First();
        return counts.Count > 1 ? counts.Sep : ',';
    }

    private static string FirstNonEmptyLine(string text)
    {
        var start = 0;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                quoted = !quoted;
            }
            else if (!quoted && c is '\r' or '\n')
            {
                var line = text[start..i];
                if (!string.IsNullOrWhiteSpace(line)) return line;
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                start = i + 1;
            }
        }
        return start < text.Length ? text[start..] : "";
    }

    private static List<string> SplitLine(string line, char separator)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (!quoted && c == separator)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        cells.Add(current.ToString());
        return cells;
    }
}
