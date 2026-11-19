using System.Collections.Immutable;

namespace GsmKurswahl.ImportExport;

public static class StudentColumnMatcher
{
    private static readonly (string Field, string[] Aliases)[] FieldAliases =
    [
        ("studentId", ["asv-id", "asv_id", "schüler-id", "schueler-id", "quell-id", "id", "dauerhafte schüler-id"]),
        ("firstName", ["vorname", "vornamen", "rufname"]),
        ("lastName", ["nachname", "familienname"]),
        ("name", ["name mit vornamen", "name", "anzeigename"]),
        ("class", ["klasse", "klassengruppe"]),
        ("year", ["abiturjahrgang", "jahrgang", "abijahr"]),
        ("username", ["benutzername", "windows-benutzername", "konto"]),
        ("religion", ["religion", "religionsunterricht", "religion/ethik", "ethik/religion"]),
        ("languages", ["fremdsprachen", "sprachen", "sprachlaufbahn"]),
        ("italian", ["italienisch", "italienisch-profil"]),
        ("bridge", ["brückenkurs", "brueckenkurs", "informatik-brückenkurs"]),
        ("imp", ["imp"]),
        ("bilingual", ["bilingual", "durchgängig bilingual"]),
        ("sport", ["sportbefreiung", "sport"]),
        ("departure", ["abgänger", "abgaenger", "abgängerstatus"])
    ];

    public static Dictionary<string, string> Match(ImmutableArray<string> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in StudentTableImport.Fields)
        {
            var exact = headers.FirstOrDefault(h => string.Equals(h, field, StringComparison.Ordinal));
            if (exact is not null && used.Add(exact))
                result[field] = exact;
        }

        foreach (var (field, aliases) in FieldAliases)
        {
            if (result.ContainsKey(field)) continue;
            foreach (var alias in aliases)
            {
                var matched = headers.FirstOrDefault(h => !used.Contains(h) && string.Equals(h, alias, StringComparison.OrdinalIgnoreCase));
                if (matched is not null)
                {
                    used.Add(matched);
                    result[field] = matched;
                    break;
                }
            }
        }

        return result;
    }
}
