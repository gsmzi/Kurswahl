using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Rules;

public sealed record RequiredCourse(string SubjectId, ImmutableArray<int> Semesters);

// Preliminary planning from the supplied specification, not an admission or crediting decision.
public static class ChoiceWorkflow
{
    private static readonly string[] Core = ["D", "M", "E", "BIO", "CH", "PH"];
    private static readonly string[] Sciences = ["BIO", "CH", "PH"];
    public static readonly ImmutableArray<RuleDefinition> Definitions =
    [
        new("PLAN-LF", "Drei LF mit mindestens zwei Kernfächern", "Pflichtenheft §6 Nr.1; Nutzerablauf 25.10.2026", "M1-Demo-Wahlassistent", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-ORAL", "Zwei mündliche Planungsfächer; D/M und Aufgabenfelder", "Pflichtenheft §6 Nr.8,11", "M1-Demo-Prüfungsplanung, keine Zulassung", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-REQUIRED", "Eindeutige Pflichtbelegung ergänzen, Alternativen offenlassen", "Pflichtenheft §4; §6 Nr.3,4,8,10", "M1-Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview)
    ];

    private static string[] Lf(CourseChoice choice) => choice.Selections.Where(s => s.Type == CourseType.Lf).Select(s => s.SubjectId).ToArray();
    private static int Field(string subject) => subject switch
    {
        "D" or "E" or "BK" or "MU" => 1,
        "G" or "GK" or "GEO" or "W" or "ETH" or "ER" or "KR" => 2,
        "M" or "BIO" or "CH" or "PH" or "INF" => 3,
        _ => 0
    };

    public static ImmutableArray<string> ForcedOral(CourseChoice choice) =>
        [.. new[] { "D", "M" }.Except(Lf(choice))];

    public static ImmutableArray<string> OralCandidates(CourseChoice choice, OfferCatalog catalog) =>
        [.. catalog.Offers.Where(o => o.Type == CourseType.Bf && o.SubjectId != "GEO"
            && (Field(o.SubjectId) != 0 || o.SubjectId == "SP") && !Lf(choice).Contains(o.SubjectId)
            && new ChoiceValidator().GetAvailability(o, ProfileContext.Missing).CanSelect
            && OralSemesters(choice, o.SubjectId).All(h => o.Schedule.Any(s => s.Semester == h)))
            .Select(o => o.SubjectId).Distinct()];

    private static bool Combination(CourseChoice choice, IEnumerable<string> oral)
    {
        var all = Lf(choice).Concat(oral).ToArray();
        return all.Contains("D") && all.Contains("M") && new[] { 1, 2, 3 }.All(f => all.Any(s => Field(s) == f));
    }

    public static string? LfError(CourseChoice choice, OfferCatalog catalog)
    {
        var lf = Lf(choice);
        if (lf.Length != 3 || lf.Distinct().Count() != 3) return "PLAN-LF: Bitte genau drei verschiedene Leistungsfächer wählen.";
        if (lf.Any(s => Field(s) == 0 && s != "SP")) return "PLAN-LF: needs_review – Fachzuordnung außerhalb des bekannten Demoangebots klären.";
        if (lf.Count(Core.Contains) < 2) return "PLAN-LF: Mindestens zwei LF aus Deutsch, Mathematik, Englisch oder BIO/CH/PH wählen. Weitere Sprachlaufbahnen: needs_review.";
        if (new ChoiceValidator().Validate(choice, catalog, ProfileContext.Missing).Messages.Any(m =>
            m.Rule.Severity == ReviewStatus.Invalid && m.SubjectIds.Any(lf.Contains)))
            return "PLAN-LF: Angebot oder Belegung der Leistungsfächer korrigieren.";
        var candidates = OralCandidates(choice, catalog);
        if (!candidates.Any(a => candidates.Any(b => a != b && Combination(choice, [a, b]))))
            return "PLAN-ORAL: Mit diesen LF ist im verfügbaren Demoangebot keine Kombination für Deutsch, Mathematik und alle Aufgabenfelder möglich.";
        return null;
    }

    public static string? OralError(CourseChoice choice, OfferCatalog catalog)
    {
        var oral = choice.PlannedOralSubjects;
        if (oral.Length != 2 || oral.Distinct().Count() != 2) return "PLAN-ORAL: Zwei verschiedene mündliche Prüfungsfächer planen.";
        if (oral.Any(s => !OralCandidates(choice, catalog).Contains(s))) return "PLAN-ORAL: Prüfungsplanung passt nicht mehr zu LF oder Angebot. Bitte neu wählen.";
        if (!Combination(choice, oral)) return "PLAN-ORAL: Deutsch, Mathematik und alle drei Aufgabenfelder müssen abgedeckt sein.";
        return null;
    }

    public static ImmutableArray<int> OralSemesters(CourseChoice choice, string subject) =>
        subject == "GK" && Lf(choice).Contains("W") ? [1, 3, 4] : [1, 2, 3, 4];

    public static ImmutableArray<RequiredCourse> Required(CourseChoice choice)
    {
        var result = new Dictionary<string, ImmutableArray<int>>();
        foreach (var subject in new[] { "D", "M", "E", "G", "SP" }) result[subject] = [1, 2, 3, 4];
        result["GK"] = Lf(choice).Contains("W") ? [1] : [1, 2];
        result["GEO"] = Lf(choice).Contains("W") ? [3] : [3, 4];
        foreach (var subject in choice.PlannedOralSubjects) result[subject] = OralSemesters(choice, subject);
        return [.. result.Where(r => !Lf(choice).Contains(r.Key)).Select(r => new RequiredCourse(r.Key, r.Value))];
    }

    public static ImmutableArray<string> Remaining(CourseChoice choice)
    {
        bool Four(string s) => choice.Selections.Any(c => c.SubjectId == s && c.Semesters.Length == 4);
        var notes = ImmutableArray.CreateBuilder<string>();
        foreach (var required in Required(choice))
            if (!choice.Selections.Any(s => s.SubjectId == required.SubjectId && required.Semesters.All(s.Semesters.Contains)))
                notes.Add($"PLAN-REQUIRED: {required.SubjectId} benötigt HJ {string.Join(", ", required.Semesters)}; Belegung ergänzen oder Angebot klären.");
        if (!Four("E")) notes.Add("Fortgeführte Fremdsprache wählen (im Demoangebot Englisch; andere Sprachlaufbahnen klären).");
        if (!Sciences.Any(Four)) notes.Add("Mindestens eine Naturwissenschaft BIO/CH/PH für HJ 1–4 wählen.");
        if (Sciences.Count(Four) < 2) notes.Add("Zweite Naturwissenschaft oder zweite geeignete Fremdsprache ergänzen; INF-Alternative erst mit geprüftem Profil.");
        if (!Four("BK") && !Four("MU")) notes.Add("Kunst oder Musik für HJ 1–4 wählen.");
        if (!new[] { "ETH", "ER", "KR" }.Any(Four)) notes.Add("Religion/Ethik für HJ 1–4 wählen; persönliche Voraussetzungen bleiben zu klären.");
        if (choice.Selections.Sum(s => s.Semesters.Length) < 42) notes.Add("Weitere Belegungen ergänzen: Planungsziel mindestens 42 Halbjahreskurse (12 LF + 30 weitere).");
        notes.Add("needs_review: Sportbefreiung/Ersatz, Religion/Ethik, persönliche Voraussetzungen und Anrechnung (höchstens 40) werden erst in M2 vollständig geprüft.");
        return notes.ToImmutable();
    }
}
