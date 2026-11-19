using System.Collections.Immutable;

namespace GsmKurswahl.ImportExport;

/// <summary>
/// Adapter for the school's ASV-BW export format „GSM Export für Kurswahl“ (one row per class-10 pupil).
/// It only translates ASV codes into the neutral student-table fields; all validation stays in
/// <see cref="StudentTableImport.Preview"/>. Mapping version: <see cref="MappingVersion"/>.
/// </summary>
public static class AsvStudentImport
{
    public const string FormatName = "GSM Export für Kurswahl";
    public const string MappingVersion = "asv-gsm-kurswahl-v1";
    public const string BridgeCourseCode = "BRK-INF";

    private static readonly string[] IdHeaders = ["ASV-ID", "Schüler-ID"];
    private static readonly string[] LastNameHeaders = ["Familienname"];
    private static readonly string[] FirstNameHeaders = ["Rufname"];
    private static readonly string[] ClassHeaders = ["Klasse"];
    private static readonly string[] GroupHeaders = ["Klassengruppe"];
    private static readonly string[] ProfileHeaders = ["Profil", "1. Schülerprofil Profilfach.Kurzform"];
    private static readonly string[] SubjectHeaders = ["Unterrichtsfächer", "Alle besuchten Unterrichtsfächer"];

    // ASV-BW subject codes for religion/ethics in class 10 → internal subject ids.
    private static readonly IReadOnlyDictionary<string, string> Religion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    { ["ETH"] = "ETH", ["REV"] = "ER", ["RRK"] = "KR" };
    // Foreign languages attended in class 10 (ASV codes equal internal ids). English is always included by the rules.
    private static readonly string[] Languages = ["E", "F", "L", "I", "GR"];

    public static readonly string[] OutputHeaders = ["studentId", "lastName", "firstName", "class", "year", "italian", "bridge", "imp", "bilingual", "religion", "languages"];

    /// <summary>True when the table carries the characteristic columns of „GSM Export für Kurswahl“.</summary>
    public static bool Detect(ImportTable table) =>
        Find(table, IdHeaders) >= 0 && Find(table, ProfileHeaders) >= 0 && Find(table, SubjectHeaders) >= 0;

    /// <summary>Translates the ASV export into the neutral student-table fields for the opened graduation year.</summary>
    public static ImportTable Convert(ImportTable table, int graduationYear)
    {
        if (!Detect(table)) throw new InvalidDataException($"Keine Spalten des ASV-Formats „{FormatName}“ gefunden (ASV-ID, Profil, Unterrichtsfächer).");
        int Required(string[] names) => Find(table, names) is var i && i >= 0 ? i : throw new InvalidDataException($"ASV-Format „{FormatName}“: Spalte „{names[0]}“ fehlt.");
        var id = Required(IdHeaders); var last = Required(LastNameHeaders); var first = Required(FirstNameHeaders); var cls = Required(ClassHeaders);
        var profile = Required(ProfileHeaders); var subjects = Required(SubjectHeaders); var group = Find(table, GroupHeaders);
        var year = graduationYear.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var rows = table.Rows.Select(row =>
        {
            string Cell(int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";
            var p = Cell(profile).ToUpperInvariant();
            var subjectList = Cell(subjects);
            var codes = subjectList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant()).Distinct().ToArray();
            var known = codes.Length > 0;
            string YesNo(bool value) => value ? "ja" : "nein";

            var italian = p.Length == 0 ? "" : YesNo(p == "I");
            var imp = p.Length == 0 ? "" : YesNo(p == "IMP");
            // The ASV subject list is complete: a missing BRK-INF means the bridge course was not attended
            // (user decision 19.11.2026, also in years without a bridge course offer).
            var bridge = known ? YesNo(codes.Contains(BridgeCourseCode)) : "";
            var bilingual = Cell(group).ToLowerInvariant() switch { "bili" => "ja", "reg" => "nein", _ => "" };
            var religions = codes.Where(Religion.ContainsKey).Select(c => Religion[c]).Distinct().ToArray();
            // Several religion/ethics codes are not resolved silently; the neutral import rejects the row.
            var religion = religions.Length switch { 0 => "", 1 => religions[0], _ => "mehrdeutig: " + string.Join("/", religions) };
            var languages = string.Join(",", codes.Where(c => Languages.Contains(c)));
            return ImmutableArray.Create(Cell(id), Cell(last), Cell(first), Cell(cls), year, italian, bridge, imp, bilingual, religion, languages);
        }).ToImmutableArray();
        return new([.. OutputHeaders], rows);
    }

    private static int Find(ImportTable table, string[] names)
    {
        foreach (var name in names)
        {
            var index = table.Headers.IndexOf(table.Headers.FirstOrDefault(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase)) ?? "\0");
            if (index >= 0) return index;
        }
        return -1;
    }
}
