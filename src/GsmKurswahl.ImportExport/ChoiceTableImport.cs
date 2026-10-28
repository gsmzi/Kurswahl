using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

public static class ChoiceTableImport
{
    public static readonly string[] Fields = ["studentId", "year", "subject", "type", "variant", "semester"];

    // One row per subject/type/variant/semester. A broken row rejects the complete preview,
    // so a partial file cannot silently erase the remainder of a person's choice.
    public static ImmutableArray<ExternalChoice> Read(ImportTable table, IReadOnlyDictionary<string, string> mapping, OfferCatalog catalog)
    {
        if (Fields.Any(f => !mapping.TryGetValue(f, out var h) || !table.Headers.Contains(h))
            || mapping.Count != Fields.Length || mapping.Values.Distinct().Count() != mapping.Count)
            throw new ArgumentException("Alle sechs Felder müssen verschiedenen vorhandenen Spalten zugeordnet werden.");
        var rows = new List<(string Id, CourseOffer Offer, int Semester)>();
        for (var i = 0; i < table.Rows.Length; i++)
        {
            var row = table.Rows[i];
            string Value(string field) { var col = table.Headers.IndexOf(mapping[field]); return col < row.Length ? row[col].Trim() : ""; }
            try
            {
                if (row.Length > table.Headers.Length) throw new ArgumentException("Mehr Werte als Spalten.");
                var id = Value("studentId");
                if (!id.StartsWith("demo-", StringComparison.Ordinal)) throw new ArgumentException("Nur synthetische demo-IDs erlaubt; keine Namenszuordnung.");
                if (!int.TryParse(Value("year"), out var year) || year != catalog.GraduationYear) throw new ArgumentException("Abiturjahrgang stimmt nicht überein.");
                var type = Value("type").ToLowerInvariant() switch { "lf" => CourseType.Lf, "bf" => CourseType.Bf, "elective" => CourseType.Elective, "seminar" => CourseType.Seminar, _ => throw new ArgumentException("Kursart: Lf, Bf, Elective oder Seminar erforderlich.") };
                var variant = Value("variant").ToLowerInvariant() switch { "normal" => CourseVariant.Normal, "bilingual" => CourseVariant.Bilingual, _ => throw new ArgumentException("Variante Normal oder Bilingual erforderlich.") };
                if (!int.TryParse(Value("semester"), out var semester) || semester is < 1 or > 4) throw new ArgumentException("Halbjahr 1–4 erforderlich.");
                var offers = catalog.Offers.Where(o => o.SubjectId == Value("subject") && o.Type == type && o.Variant == variant).ToArray();
                if (offers.Length != 1) throw new ArgumentException("Fach/Kursart/Variante ist nicht eindeutig im Katalog vorhanden.");
                rows.Add((id, offers[0], semester));
            }
            catch (ArgumentException ex) { throw new InvalidDataException($"Zeile {i + 2}: {ex.Message}", ex); }
        }
        if (rows.Count == 0) throw new InvalidDataException("Keine Belegungen vorhanden.");
        if (rows.GroupBy(r => (r.Id, r.Offer.Id, r.Semester)).Any(g => g.Count() > 1)) throw new InvalidDataException("Doppelte Belegung: Schüler-ID, Angebot und Halbjahr müssen eindeutig sein.");
        if (rows.Select(r => r.Id).Distinct().GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new InvalidDataException("Mehrdeutige Schüler-IDs (Groß-/Kleinschreibung).");
        return [.. rows.GroupBy(r => r.Id).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new ExternalChoice(g.Key, catalog.GraduationYear,
            [.. g.GroupBy(r => r.Offer).OrderBy(o => o.Key.Id, StringComparer.Ordinal).Select(o => new CourseSelection(o.Key.Id, o.Key.SubjectId, o.Key.Type, o.Key.Variant, [.. o.Select(r => r.Semester).Order()]))]))];
    }
}
