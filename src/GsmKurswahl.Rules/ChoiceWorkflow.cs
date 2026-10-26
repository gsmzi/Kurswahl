using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Rules;

public sealed record RequiredCourse(string SubjectId, ImmutableArray<int> Semesters);
public sealed record RestrictedOffer(string OfferId, string Explanation);

// An open planning decision. Choose = how many options the student still decides about beyond
// what LF already cover; Open = how many are still missing. Only a planning aid, never an admission.
public sealed record ChoiceTask(string Id, RuleDefinition Rule, string Title, string Hint, ImmutableArray<int> Semesters,
    int Choose, int Open, ImmutableArray<string> OptionOfferIds, ImmutableArray<string> ChosenOfferIds,
    ImmutableArray<string> FulfilledBySubjects, ImmutableArray<RestrictedOffer> Restricted)
{
    public bool IsSatisfied => Open == 0;
    public bool IsExclusive => Choose == 1;
}

// Preliminary planning from the supplied specification, not an admission or crediting decision.
public static class ChoiceWorkflow
{
    private static readonly string[] Core = ["D", "M", "E", "BIO", "CH", "PH"];
    private static readonly string[] Sciences = ["BIO", "CH", "PH"];
    public const int MinimumCourses = 42;
    public static readonly ImmutableArray<RuleDefinition> Definitions =
    [
        new("PLAN-LF", "Drei LF mit mindestens zwei Kernfächern", "Pflichtenheft §6 Nr.1; Nutzerablauf 25.10.2026", "M1-Demo-Wahlassistent", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-ORAL", "Zwei mündliche Planungsfächer; D/M und Aufgabenfelder", "Pflichtenheft §6 Nr.8,11", "M1-Demo-Prüfungsplanung, keine Zulassung", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-REQUIRED", "Eindeutige Pflichtbelegung ergänzen, Alternativen offenlassen", "Pflichtenheft §4; §6 Nr.3,4,8,10", "M1-Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-ART", "Kunst oder Musik mit vier Kursen planen", "Pflichtenheft §6 Nr.3", "M1-Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-RELIGION", "Religion oder Ethik mit vier Kursen planen; Zulässigkeit offen", "Pflichtenheft §6 Nr.3,9", "M1-Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-SCIENCE", "Eine Naturwissenschaft, dazu zweite Naturwissenschaft (INF nur als zweite) planen", "Pflichtenheft §6 Nr.3,4,5", "M1-Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-MIN-COURSES", "Planungsziel mindestens 42 Halbjahreskurse", "Pflichtenheft §6 Nr.2", "M1-Demo-Belegungsplanung, keine Anrechnung", ChoiceValidator.Version, ReviewStatus.NeedsReview)
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

    public static ImmutableArray<string> OralCandidates(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null) =>
        [.. catalog.Offers.Where(o => o.Type == CourseType.Bf && o.SubjectId != "GEO"
            && (Field(o.SubjectId) != 0 || o.SubjectId == "SP") && !Lf(choice).Contains(o.SubjectId)
            && new ChoiceValidator().GetAvailability(o, profile ?? ProfileContext.Missing).CanSelect
            && OralSemesters(choice, o.SubjectId).All(h => o.Schedule.Any(s => s.Semester == h)))
            .Select(o => o.SubjectId).Distinct()];

    private static bool Combination(CourseChoice choice, IEnumerable<string> oral)
    {
        var all = Lf(choice).Concat(oral).ToArray();
        return all.Contains("D") && all.Contains("M") && new[] { 1, 2, 3 }.All(f => all.Any(s => Field(s) == f));
    }

    public static string? LfError(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var context = profile ?? ProfileContext.Missing;
        var lf = Lf(choice);
        if (lf.Length != 3 || lf.Distinct().Count() != 3) return "PLAN-LF: Bitte genau drei verschiedene Leistungsfächer wählen.";
        if (lf.Any(s => Field(s) == 0 && s != "SP")) return "PLAN-LF: needs_review – Fachzuordnung außerhalb des bekannten Demoangebots klären.";
        if (lf.Count(Core.Contains) < 2) return "PLAN-LF: Mindestens zwei LF aus Deutsch, Mathematik, Englisch oder BIO/CH/PH wählen. Weitere Sprachlaufbahnen: needs_review.";
        if (new ChoiceValidator().Validate(choice, catalog, context).Messages.Any(m =>
            m.Rule.Severity == ReviewStatus.Invalid && m.SubjectIds.Any(lf.Contains)))
            return "PLAN-LF: Angebot oder Belegung der Leistungsfächer korrigieren.";
        var candidates = OralCandidates(choice, catalog, context);
        if (!candidates.Any(a => candidates.Any(b => a != b && Combination(choice, [a, b]))))
            return "PLAN-ORAL: Mit diesen LF ist im verfügbaren Demoangebot keine Kombination für Deutsch, Mathematik und alle Aufgabenfelder möglich.";
        return null;
    }

    public static string? OralError(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var oral = choice.PlannedOralSubjects;
        if (oral.Length != 2 || oral.Distinct().Count() != 2) return "PLAN-ORAL: Zwei verschiedene mündliche Prüfungsfächer planen.";
        if (oral.Any(s => !OralCandidates(choice, catalog, profile).Contains(s))) return "PLAN-ORAL: Prüfungsplanung passt nicht mehr zu LF oder Angebot. Bitte neu wählen.";
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

    // Oral candidates that still allow a complete D/M/Aufgabenfeld combination with the current planning.
    public static ImmutableArray<string> UsefulOralCandidates(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var candidates = OralCandidates(choice, catalog, profile);
        var planned = choice.PlannedOralSubjects.Where(candidates.Contains).ToArray();
        if (planned.Length >= 2) return [];
        return [.. candidates.Where(c => !planned.Contains(c) && (planned.Length == 1
            ? Combination(choice, [planned[0], c])
            : candidates.Any(d => d != c && Combination(choice, [c, d]))))];
    }

    public static int CountedCourses(CourseChoice choice) => choice.Selections.Sum(s => s.Semesters.Length);

    public static ImmutableArray<ChoiceTask> Tasks(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var validator = new ChoiceValidator();
        var requiredSubjects = Required(choice).Select(r => r.SubjectId).ToHashSet();
        ImmutableArray<int> four = [1, 2, 3, 4];
        ChoiceTask Area(string ruleId, string title, string hint, string[] subjects, int needed, Func<string[], int> fulfilled)
        {
            var lf = choice.Selections.Where(s => s.Type == CourseType.Lf && subjects.Contains(s.SubjectId)).Select(s => s.SubjectId).ToImmutableArray();
            var choose = Math.Max(0, needed - lf.Length);
            var matching = choice.Selections.Where(s => s.Type != CourseType.Lf && subjects.Contains(s.SubjectId) && four.All(s.Semesters.Contains))
                .OrderByDescending(s => requiredSubjects.Contains(s.SubjectId))
                .ThenByDescending(s => Sciences.Contains(s.SubjectId))
                .Take(choose)
                .ToArray();
            var chosen = matching.Select(s => s.OfferId).ToImmutableArray();
            var candidates = catalog.Offers.Where(o => o.Type != CourseType.Lf && subjects.Contains(o.SubjectId) && !lf.Contains(o.SubjectId)
                && four.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
            var options = candidates.Where(o => chosen.Contains(o.Id) || validator.GetAvailability(o, profile).CanSelect).Select(o => o.Id).ToImmutableArray();
            var restricted = candidates.Where(o => !options.Contains(o.Id))
                .Select(o => new RestrictedOffer(o.Id, validator.GetAvailability(o, profile).Explanation)).ToImmutableArray();
            var have = lf.Concat(matching.Select(s => s.SubjectId)).ToArray();
            return new(ruleId, Definitions.Single(d => d.Id == ruleId), title, hint, four, choose,
                Math.Max(0, needed - fulfilled(have)), options, chosen, lf, restricted);
        }
        return
        [
            Area("PLAN-AREA-ART", "Kunst oder Musik", "Wähle eines der beiden Fächer für HJ 1–4.", ["BK", "MU"], 1, have => have.Length),
            Area("PLAN-AREA-RELIGION", "Religion oder Ethik", "Wähle ein Fach für HJ 1–4. Ob du es belegen darfst (Konfession, Vorbildung), klärt die Oberstufenberatung – needs_review.",
                ["ER", "KR", "ETH"], 1, have => have.Length),
            // INF may replace only the second science (Pflichtenheft §6 Nr.4); a second language alternative stays needs_review.
            Area("PLAN-AREA-SCIENCE", "Naturwissenschaften", "Du brauchst zwei Naturwissenschaften mit HJ 1–4. Eine zweite fortgeführte Fremdsprache oder Informatik als Alternative klärt die Oberstufenberatung.",
                [.. Sciences, "INF"], 2, have => Math.Min(2, have.Count(Sciences.Contains) + (have.Contains("INF") ? 1 : 0)))
        ];
    }

    public static ImmutableArray<string> Remaining(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var notes = ImmutableArray.CreateBuilder<string>();
        foreach (var required in Required(choice))
            if (!choice.Selections.Any(s => s.SubjectId == required.SubjectId && required.Semesters.All(s.Semesters.Contains)))
                notes.Add($"PLAN-REQUIRED: {required.SubjectId} benötigt HJ {string.Join(", ", required.Semesters)}; Belegung ergänzen oder Angebot klären.");
        foreach (var task in Tasks(choice, catalog, profile).Where(t => !t.IsSatisfied))
            notes.Add($"{task.Rule.Id}: {task.Title} – noch {task.Open} offen.");
        var count = CountedCourses(choice);
        if (count < MinimumCourses)
            notes.Add($"PLAN-MIN-COURSES: {count} von mindestens {MinimumCourses} Halbjahreskursen (12 LF + 30 weitere) geplant.");
        notes.Add("needs_review: Sportbefreiung/Ersatz, Religion/Ethik, persönliche Voraussetzungen und Anrechnung (höchstens 40) werden erst in M2 vollständig geprüft.");
        return notes.ToImmutable();
    }
}
