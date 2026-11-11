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
        new("PLAN-LF", "Drei LF mit mindestens zwei Kernfächern", "Pflichtenheft §6 Nr.1; Nutzerablauf 25.10.2026", "Demo-Wahlassistent", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-ORAL", "Zwei mündliche Planungsfächer; D/M und Aufgabenfelder", "Pflichtenheft §6 Nr.8,11", "Demo-Prüfungsplanung, keine Zulassung", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("PLAN-REQUIRED", "Eindeutige Pflichtbelegung ergänzen, Alternativen offenlassen", "Pflichtenheft §4; §6 Nr.3,4,8,10", "Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-ART", "Kunst oder Musik mit vier Kursen planen", "Pflichtenheft §6 Nr.3", "Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-RELIGION", "Religion oder Ethik mit vier Kursen planen; Zulässigkeit offen", "Pflichtenheft §6 Nr.3,9", "Demo-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-LANGUAGE", "Mindestens eine fortgeführte Fremdsprache", "Pflichtenheft §6 Nr.3,4,6", "GSM-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-SCIENCE", "Mindestens eine Naturwissenschaft (BIO/CH/PH)", "Pflichtenheft §6 Nr.3,4,5", "GSM-Belegungsplanung", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-AREA-BREADTH", "Zweite Sprache oder zweite Naturwissenschaft", "Pflichtenheft §6 Nr.4–6", "Persönlich bestätigte Sprachlaufbahnen", ChoiceValidator.Version, ReviewStatus.NeedsReview),
        new("PLAN-MIN-COURSES", "Planungsziel mindestens 42 Halbjahreskurse", "Pflichtenheft §6 Nr.2", "Demo-Belegungsplanung, keine Anrechnung", ChoiceValidator.Version, ReviewStatus.NeedsReview)
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
            && (AcademicRules.Field(o.SubjectId, profile ?? ProfileContext.Missing, catalog) != 0 || o.SubjectId == "SP") && !Lf(choice).Contains(o.SubjectId)
            && new ChoiceValidator().GetAvailability(o, profile ?? ProfileContext.Missing).CanSelect
            && !(o.SubjectId == "SP" && profile?.Status == ProfileStatus.Verified
                && (profile.Prerequisites.PermanentSportExemption == Knowledge.Yes || profile.Prerequisites.SportExemptSemesters is { Length: > 0 }))
            && !(o.SubjectId is "ETH" or "ER" or "KR" && profile?.Status == ProfileStatus.Verified
                // A change against class 10 stays possible with an assessment (AGVO §21 Abs.2 Nr.4/5); reported as AGVO-RELIGION.
                && profile.Prerequisites.ReligionEthicsExamPermission == Knowledge.No)
            && OralSemesters(choice, o.SubjectId).All(h => o.Schedule.Any(s => s.Semester == h)))
            .Select(o => o.SubjectId).Distinct()];

    private static bool Combination(CourseChoice choice, IEnumerable<string> oral, ProfileContext? profile = null, OfferCatalog? catalog = null)
    {
        var all = Lf(choice).Concat(oral).ToArray();
        return !all.Any(a => all.Any(b => AcademicRules.MutuallyExclusiveSubjects(a, b)))
            && all.Contains("D") && all.Contains("M") && new[] { 1, 2, 3 }.All(f => all.Any(s => AcademicRules.Field(s, profile ?? ProfileContext.Missing, catalog) == f));
    }

    public static string? LfError(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var context = profile ?? ProfileContext.Missing;
        var lf = Lf(choice);
        var sciences = AcademicRules.ScienceSubjects(catalog);
        if (lf.Length != 3 || lf.Distinct().Count() != 3) return "PLAN-LF: Bitte genau drei verschiedene Leistungsfächer wählen.";
        if (lf.Any(s => AcademicRules.Field(s, context, catalog) == 0 && s != "SP")) return "PLAN-LF: needs_review – Fachzuordnung außerhalb des bekannten Demoangebots klären.";
        if (lf.Count(s => Core.Contains(s) || sciences.Contains(s) || AcademicRules.Languages(context, catalog).Contains(s)) < 2) return "PLAN-LF: Mindestens zwei LF aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder BIO/CH/PH wählen.";
        if (choice.Selections.Where(s => s.Type == CourseType.Lf).Any(s => s.Semesters.Length != 4
            || !catalog.Offers.Any(o => o.Id == s.OfferId && o.SubjectId == s.SubjectId && o.Type == s.Type
                && o.Variant == s.Variant && new ChoiceValidator().GetAvailability(o, context).CanSelect)))
            return "PLAN-LF: Angebot oder Belegung der Leistungsfächer korrigieren.";
        var candidates = OralCandidates(choice, catalog, context);
        if (!candidates.Any(a => candidates.Any(b => a != b && Combination(choice, [a, b], context, catalog))))
            return "PLAN-ORAL: Mit diesen LF ist im verfügbaren Demoangebot keine Kombination für Deutsch, Mathematik und alle Aufgabenfelder möglich.";
        return null;
    }

    public static string? OralError(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var oral = choice.PlannedOralSubjects;
        if (oral.Any(a => oral.Any(b => AcademicRules.MutuallyExclusiveSubjects(a, b))))
            return "GSM-RELIGION-EXCLUSIVE: Nur eines der Fächer Ethik, evangelische oder katholische Religion als mündliches Prüfungsfach planen.";
        if (oral.Length != 2 || oral.Distinct().Count() != 2) return "PLAN-ORAL: Zwei verschiedene mündliche Prüfungsfächer planen.";
        if (oral.Any(s => !OralCandidates(choice, catalog, profile).Contains(s))) return "PLAN-ORAL: Prüfungsplanung passt nicht mehr zu LF oder Angebot. Bitte neu wählen.";
        if (!Combination(choice, oral, profile, catalog)) return "PLAN-ORAL: Deutsch, Mathematik und alle drei Aufgabenfelder müssen abgedeckt sein.";
        return null;
    }

    public static ImmutableArray<int> OralSemesters(CourseChoice choice, string subject) =>
        [1, 2, 3, 4]; // GSM requires uninterrupted GK also with LF economics (29.10.2026).

    public static ImmutableArray<RequiredCourse> Required(CourseChoice choice, ProfileContext? profile = null)
    {
        var result = new Dictionary<string, ImmutableArray<int>>();
        foreach (var subject in new[] { "D", "M", "G", "SP" }) result[subject] = [1, 2, 3, 4];
        if (profile?.Status == ProfileStatus.Verified && profile.Prerequisites.SportExemptSemesters is { } exemptions)
            result["SP"] = [.. Enumerable.Range(1, 4).Except(exemptions)];
        result["GK"] = Lf(choice).Contains("W") ? [1] : [1, 2];
        result["GEO"] = Lf(choice).Contains("W") ? [3] : [3, 4];
        foreach (var subject in choice.PlannedOralSubjects)
            if (!(subject == "SP" && profile?.Status == ProfileStatus.Verified && profile.Prerequisites.SportExemptSemesters is { Length: > 0 }))
                result[subject] = OralSemesters(choice, subject);
        return [.. result.Where(r => !Lf(choice).Contains(r.Key) && !r.Value.IsEmpty).Select(r => new RequiredCourse(r.Key, r.Value))];
    }

    // Oral candidates that still allow a complete D/M/Aufgabenfeld combination with the current planning.
    public static ImmutableArray<string> UsefulOralCandidates(CourseChoice choice, OfferCatalog catalog, ProfileContext? profile = null)
    {
        var candidates = OralCandidates(choice, catalog, profile);
        var planned = choice.PlannedOralSubjects.Where(candidates.Contains).ToArray();
        if (planned.Length >= 2) return [];
        return [.. candidates.Where(c => !planned.Contains(c) && (planned.Length == 1
            ? Combination(choice, [planned[0], c], profile, catalog)
            : candidates.Any(d => d != c && Combination(choice, [c, d], profile, catalog))))];
    }

    public static int CountedCourses(CourseChoice choice) => choice.Selections.Sum(s => s.Semesters.Length);

    public static ImmutableArray<ChoiceTask> Tasks(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var validator = new ChoiceValidator();
        var requiredSubjects = Required(choice, profile).Select(r => r.SubjectId).ToHashSet();
        ImmutableArray<int> four = [1, 2, 3, 4];
        ChoiceTask Area(string ruleId, string title, string hint, string[] subjects, int needed, Func<string[], int> fulfilled)
        {
            var lf = choice.Selections.Where(s => s.Type == CourseType.Lf && subjects.Contains(s.SubjectId)).Select(s => s.SubjectId).ToImmutableArray();
            var choose = Math.Max(0, needed - lf.Length);
            // Unknown offers (e.g. from a loaded draft) stay visible as OFFER-SELECTION but never answer a task.
            var matching = choice.Selections.Where(s => s.Type != CourseType.Lf && subjects.Contains(s.SubjectId) && four.All(s.Semesters.Contains)
                    && catalog.Offers.Any(o => o.Id == s.OfferId))
                .OrderByDescending(s => requiredSubjects.Contains(s.SubjectId))
                .ThenByDescending(s => Sciences.Contains(s.SubjectId))
                .Take(choose)
                .ToArray();
            var chosen = matching.Select(s => s.OfferId).ToImmutableArray();
            var candidates = catalog.Offers.Where(o => o.Type != CourseType.Lf && subjects.Contains(o.SubjectId) && !lf.Contains(o.SubjectId)
                && four.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
            var options = candidates.Where(o => validator.GetAvailability(o, profile).CanSelect).Select(o => o.Id).ToImmutableArray();
            var restricted = candidates.Where(o => !options.Contains(o.Id))
                .Select(o => new RestrictedOffer(o.Id, validator.GetAvailability(o, profile).Explanation)).ToImmutableArray();
            var have = lf.Concat(matching.Where(s => options.Contains(s.OfferId)).Select(s => s.SubjectId)).ToArray();
            return new(ruleId, Definitions.Single(d => d.Id == ruleId), title, hint, four, choose,
                Math.Max(0, needed - fulfilled(have)), options, chosen, lf, restricted);
        }
        var sciences = AcademicRules.ScienceSubjects(catalog);
        var tasks = new List<ChoiceTask>
        {
            Area("PLAN-AREA-ART", "Kunst oder Musik", "Wähle eines der beiden Fächer für HJ 1–4.", ["BK", "MU"], 1, have => have.Length),
            Area("PLAN-AREA-RELIGION", "Religion oder Ethik", "Wähle ein Fach für HJ 1–4. Religion wird passend zum bestätigten Klasse-10-Fach angeboten (ER: evangelisch, KR: katholisch). Bei unbekannten oder ungeprüften Angaben bleiben beide Religionsfächer vorläufig wählbar; bitte mit der Oberstufenberatung klären. Ethik bleibt wählbar; Wechsel und mündliche Prüfung ggf. klären.",
                ["ER", "KR", "ETH"], 1, have => have.Length),
            Area("PLAN-AREA-LANGUAGE", "Mindestens eine Fremdsprache", "Englisch, Französisch oder Latein für HJ 1–4. Eine passende LF-Belegung erfüllt die Pflicht bereits. Italienisch nur mit bestätigter geeigneter Sprachlaufbahn.",
                AcademicRules.Languages(profile, catalog), 1, have => have.Length),
            Area("PLAN-AREA-SCIENCE", "Mindestens eine Naturwissenschaft", "Biologie, Chemie oder Physik für HJ 1–4. Informatik ersetzt diese erste Naturwissenschaft nicht.",
                sciences, 1, have => have.Length)
        };
        var languages = AcademicRules.Languages(profile, catalog);
        var full = choice.Selections.Where(AcademicRules.Four).OrderByDescending(s => s.Type == CourseType.Lf)
            .ThenByDescending(s => requiredSubjects.Contains(s.SubjectId)).ToArray();
        var firstLanguage = tasks.Single(t => t.Id == "PLAN-AREA-LANGUAGE") is { } langTask
            ? langTask.FulfilledBySubjects.Concat(langTask.ChosenOfferIds.Select(id => choice.Selections.First(s => s.OfferId == id).SubjectId)).FirstOrDefault()
            : null;
        var firstScience = tasks.Single(t => t.Id == "PLAN-AREA-SCIENCE") is { } sciTask
            ? sciTask.FulfilledBySubjects.Concat(sciTask.ChosenOfferIds.Select(id => choice.Selections.First(s => s.OfferId == id).SubjectId)).FirstOrDefault()
            : null;
        var secondary = full.Where(s => s.SubjectId != firstLanguage && s.SubjectId != firstScience
            && (languages.Contains(s.SubjectId) || sciences.Contains(s.SubjectId) || s.SubjectId == "INF")
            && catalog.Offers.Any(o => o.Id == s.OfferId && validator.GetAvailability(o, profile).CanSelect)).FirstOrDefault();
        var satisfied = secondary is not null && (secondary.SubjectId != "INF" || firstScience is not null);
        var candidateSubjects = languages.Concat(sciences).Concat(["INF", "I"]).Concat(catalog.Subjects.Where(s => s.IsForeignLanguage).Select(s => s.Id)).Distinct().ToArray();
        var candidates = catalog.Offers.Where(o => o.Type == CourseType.Bf && candidateSubjects.Contains(o.SubjectId)
            && o.SubjectId != firstLanguage && o.SubjectId != firstScience && !Lf(choice).Contains(o.SubjectId)
            && four.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
        bool CanSelect(CourseOffer offer) => validator.GetAvailability(offer, profile).CanSelect
            && (offer.SubjectId != "I" || languages.Contains("I"));
        tasks.Add(new("PLAN-AREA-BREADTH", Definitions.Single(r => r.Id == "PLAN-AREA-BREADTH"),
            "Zweite Fremdsprache oder zweite Naturwissenschaft",
            "Wähle zusätzlich eine andere Fremdsprache oder Naturwissenschaft für HJ 1–4. Berechtigtes Informatik kann die zweite Naturwissenschaft ersetzen. Beide Wege sind gleichwertige Alternativen.",
            four, secondary?.Type == CourseType.Lf ? 0 : 1, satisfied ? 0 : 1,
            [.. candidates.Where(CanSelect).Select(o => o.Id)],
            secondary is { Type: not CourseType.Lf } ? [secondary.OfferId] : [],
            secondary?.Type == CourseType.Lf ? [secondary.SubjectId] : [],
            [.. candidates.Where(o => !CanSelect(o)).Select(o => new RestrictedOffer(o.Id,
                o.SubjectId == "I" && !languages.Contains("I") ? "Geeignete fortgeführte Italienisch-Sprachlaufbahn muss im Profil bestätigt werden." : validator.GetAvailability(o, profile).Explanation))]));
        // A subject-level oral choice does not decide between normal/bilingual offers.
        // Ask explicitly even when another course already satisfies the general area.
        foreach (var required in Required(choice, profile).Reverse())
        {
            if (choice.Selections.Any(s => s.SubjectId == required.SubjectId)) continue;
            var variants = catalog.Offers.Where(o => o.SubjectId == required.SubjectId && o.Type == CourseType.Bf
                && validator.GetAvailability(o, profile).CanSelect
                && required.Semesters.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
            if (variants.Length < 2) continue;
            var name = catalog.Subjects.Single(s => s.Id == required.SubjectId).Name;
            tasks.Insert(0, new("PLAN-VARIANT-" + required.SubjectId, Definitions.Single(r => r.Id == "PLAN-REQUIRED"),
                name + ": Kursvariante wählen", "Dieses Fach ist erforderlich. Wähle ausdrücklich die normale oder bilinguale Kursvariante.",
                required.Semesters, 1, 1, [.. variants.Select(o => o.Id)], [], [], []));
        }
        return [.. tasks];
    }

    public static ImmutableArray<string> Remaining(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var notes = ImmutableArray.CreateBuilder<string>();
        foreach (var required in Required(choice, profile))
            if (!choice.Selections.Any(s => s.SubjectId == required.SubjectId && required.Semesters.All(s.Semesters.Contains)))
                notes.Add($"PLAN-REQUIRED: {required.SubjectId} benötigt HJ {string.Join(", ", required.Semesters)}; Belegung ergänzen oder Angebot klären.");
        foreach (var task in Tasks(choice, catalog, profile).Where(t => !t.IsSatisfied))
            notes.Add($"{task.Rule.Id}: {task.Title} – noch {task.Open} offen.");
        var count = CountedCourses(choice);
        if (count < MinimumCourses)
            notes.Add($"PLAN-MIN-COURSES: {count} von mindestens {MinimumCourses} Halbjahreskursen (12 LF + 30 weitere) geplant.");
        notes.Add("Die Oberstufenberatung klärt die in den Prüfhinweisen genannten offenen Fragen. Der Regelstand für deinen Abiturjahrgang und die endgültige Anrechnung müssen fachlich bestätigt werden.");
        return notes.ToImmutable();
    }
}
