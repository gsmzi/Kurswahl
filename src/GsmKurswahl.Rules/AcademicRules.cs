using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Rules;

// Necessary conditions from the specification. Unresolved authorizations remain explicit review cases.
public static class AcademicRules
{
    public static readonly string[] Sciences = ["BIO", "CH", "PH"];
    public static readonly ImmutableArray<RuleDefinition> Definitions =
    [
        Rule("M2-RELEASE", "Jahrgangsbezogene fachliche Freigabe", "Pflichtenheft §§6,18", ReviewStatus.NeedsReview),
        Rule("AGVO-LF", "Drei LF, davon zwei Kernfächer", "AGVO §12; Pflichtenheft §6 Nr.1"),
        Rule("AGVO-COURSES", "12 LF und mindestens 30 weitere Kurse", "AGVO §§12,13; Pflichtenheft §6 Nr.2"),
        Rule("AGVO-REQUIRED", "Pflichtfächer und GSM-Halbjahresplan", "AGVO §13; Pflichtenheft §§4,6 Nr.3,8"),
        Rule("AGVO-LANGUAGE-SCIENCE", "Sprach- und Naturwissenschaftsbelegung", "AGVO §13; Pflichtenheft §6 Nr.4–6"),
        Rule("AGVO-LANGUAGE-REVIEW", "Sprachlaufbahn oder INF-Alternative ungeklärt", "AGVO §13; Pflichtenheft §6 Nr.4–6", ReviewStatus.NeedsReview),
        Rule("AGVO-SEMESTERS", "Belegung je Jahrgangsstufe", "AGVO §10; Pflichtenheft §6 Nr.12"),
        Rule("AGVO-RELIGION", "Religion/Ethik: Zustimmung und Wechsel", "AGVO §§11,21; Pflichtenheft §6 Nr.9", ReviewStatus.NeedsReview),
        Rule("AGVO-SPORT", "Befreiung, LF-Eignung und Ersatzbedarf", "AGVO §13 Abs.5; Pflichtenheft §6 Nr.10", ReviewStatus.NeedsReview),
        Rule("AGVO-SPORT-REPLACEMENT", "Zusätzliche Kurse statt Sport; kein Seminarkurs", "AGVO §13 Abs.5; ZSL Ratgeber Schulsport"),
        Rule("AGVO-EXAM", "Mindestens eine mögliche Prüfungskombination", "AGVO §21; Pflichtenheft §6 Nr.11"),
        Rule("AGVO-CREDIT", "Pflichtanrechnung höchstens 40", "AGVO §§17,21; Pflichtenheft §6 Nr.11", ReviewStatus.NeedsReview),
        Rule("AGVO-ELECTION", "Vorwahl, Endwahl und Wahltermin", "AGVO §14; Pflichtenheft §6 Nr.13", ReviewStatus.NeedsReview),
        Rule("PLAN-HOURS", "32 Wochenstunden als Planungshinweis", "Pflichtenheft §6 letzter Absatz", ReviewStatus.NeedsReview)
    ];
    private static RuleDefinition Rule(string id, string description, string source, ReviewStatus severity = ReviewStatus.Invalid) =>
        new(id, description, source, "GSM-Kurswahl; Rechtsstand für Abiturjahrgang vor Freigabe bestätigen", ChoiceValidator.Version, severity);

    public static string[] Languages(ProfileContext profile) =>
        ["E", "F", "L", .. (profile.Status == ProfileStatus.Verified ? profile.Prerequisites.ContinuedLanguages : [])
            .Where(s => s is "GR" or "I" or "SPA" or "RU" or "POR")];
    public static bool Four(CourseSelection s) => Enumerable.Range(1, 4).All(s.Semesters.Contains);
    public static int Field(string subject, ProfileContext profile) => Languages(profile).Contains(subject) ? 1 : subject switch
    {
        "D" or "BK" or "MU" => 1,
        "G" or "GK" or "GEO" or "W" or "ETH" or "ER" or "KR" => 2,
        "M" or "BIO" or "CH" or "PH" or "INF" => 3,
        _ => 0
    };

    public static ImmutableArray<RuleMessage> Check(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var messages = ImmutableArray.CreateBuilder<RuleMessage>();
        void Add(string id, string explanation, string[]? subjects = null, int[]? semesters = null) =>
            messages.Add(new(Definitions.Single(r => r.Id == id), [.. subjects ?? []], [.. semesters ?? []], explanation,
                "Belegung prüfen; offene Voraussetzungen und Sonderfälle mit der Oberstufenberatung klären."));
        Add("M2-RELEASE", "needs_review: Rechtsstand für diesen Jahrgang und interne KOE-Auslegung sind noch nicht freigegeben. Keine verbindliche Abgabe.");
        var lf = choice.Selections.Where(s => s.Type == CourseType.Lf).ToArray();
        var languages = Languages(profile);
        if (lf.Length != 3 || lf.Select(s => s.SubjectId).Distinct().Count() != 3
            || lf.Count(s => s.SubjectId is "D" or "M" || Sciences.Contains(s.SubjectId) || languages.Contains(s.SubjectId)) < 2)
            Add("AGVO-LF", "Genau drei verschiedene LF, darunter mindestens zwei aus Deutsch, Mathematik, geeigneten fortgeführten Sprachen oder BIO/CH/PH wählen.");
        if (lf.Sum(s => s.Semesters.Length) != 12 || choice.Selections.Where(s => s.Type != CourseType.Lf).Sum(s => s.Semesters.Length) < 30)
            Add("AGVO-COURSES", "Benötigt werden 12 LF-Halbjahreskurse und mindestens 30 weitere Kurse. SIA zählt nur seine zwei Halbjahre.");
        foreach (var required in ChoiceWorkflow.Required(choice, profile).Where(r => r.SubjectId is not ("E" or "SP")))
            if (!choice.Selections.Any(s => s.SubjectId == required.SubjectId && required.Semesters.All(s.Semesters.Contains)))
                Add("AGVO-REQUIRED", $"{required.SubjectId}: Pflichtbelegung fehlt.", [required.SubjectId], [.. required.Semesters]);
        foreach (var group in new[] { new[] { "BK", "MU" }, new[] { "ETH", "ER", "KR" } })
            if (!choice.Selections.Any(s => group.Contains(s.SubjectId) && Four(s)))
                Add("AGVO-REQUIRED", $"Vier Kurse in einem der Fächer {string.Join(" / ", group)} fehlen.", group, [1, 2, 3, 4]);
        var full = choice.Selections.Where(Four).Select(s => s.SubjectId).Distinct().ToArray();
        var scienceCount = full.Count(Sciences.Contains);
        var languageCount = full.Count(languages.Contains);
        var inf = full.Contains("INF") && catalog.Offers.Any(o => o.SubjectId == "INF"
            && choice.Selections.Any(s => s.OfferId == o.Id) && new ChoiceValidator().GetAvailability(o, profile).CanSelect);
        if (scienceCount < 1 || languageCount < 1 || !(languageCount >= 2 || scienceCount + (inf ? 1 : 0) >= 2))
        {
            var unknownAlternative = scienceCount > 0 && (full.Any(s => s is "I" or "F" or "L" or "GR" or "SPA" or "RU" or "POR" && !languages.Contains(s))
                || (full.Contains("INF") && (profile.Status != ProfileStatus.Verified
                    || profile.Prerequisites.InformaticsImp == Knowledge.Unknown || profile.Prerequisites.InformaticsBridge == Knowledge.Unknown)));
            Add(unknownAlternative ? "AGVO-LANGUAGE-REVIEW" : "AGVO-LANGUAGE-SCIENCE", "Eine geeignete Sprache und BIO/CH/PH sind nötig; zusätzlich zweite geeignete Sprache oder zweite Naturwissenschaft (berechtigtes INF als Alternative). Italienisch-Profil allein ist keine Sprachlaufbahnbestätigung.");
        }
        var allowedSingleSemesters = profile.Status == ProfileStatus.Verified && profile.Prerequisites.SportExemptSemesters is { } spExemptions ? spExemptions.Length : 0;
        var usedSingleSemesters = 0;
        foreach (var s in choice.Selections.Where(s => s.Type != CourseType.Lf && s.SubjectId is not ("GK" or "GEO" or "SP")))
        {
            var oddPairs = (s.Semesters.Contains(1) != s.Semesters.Contains(2) ? 1 : 0) + (s.Semesters.Contains(3) != s.Semesters.Contains(4) ? 1 : 0);
            if (oddPairs == 0) continue;
            usedSingleSemesters += oddPairs;
            if (usedSingleSemesters > allowedSingleSemesters)
                Add("AGVO-SEMESTERS", "Einzelhalbjahr benötigt eine belegte Ausnahme; regulär jahrgangsweise wählen.", [s.SubjectId], [.. s.Semesters]);
        }
        var p = profile.Status == ProfileStatus.Verified ? profile.Prerequisites : PriorEducation.Unknown;
        var chosenRelEthics = choice.Selections.Where(s => s.SubjectId is "ETH" or "ER" or "KR").Select(s => s.SubjectId).Distinct().ToArray();
        var switchedRelEthics = p.ReligionEthicsGrade10 is { } grade10 && chosenRelEthics.Any(s => s != grade10);
        if (switchedRelEthics)
            Add("AGVO-RELIGION", $"Wechsel der Belegung gegenüber Klasse 10 ({p.ReligionEthicsGrade10} -> {string.Join("/", chosenRelEthics)}): Für eine mögliche spätere mündliche Prüfung im gewechselten Fach ist eine Überprüfung/Feststellungsprüfung zu Beginn des 1. Schulhalbjahres erforderlich (AGVO § 21 Abs. 2 Nr. 4/5; bei Abmeldung Rel -> Ethik nach KOE Kap. 3.3 i. d. R. keine mündliche Prüfung möglich).", chosenRelEthics);
        else if (p.ReligionEthicsPermission != Knowledge.Yes)
            Add("AGVO-RELIGION", "Konfession, Zustimmung oder Wechsel ist offen (für eine mündliche Prüfung ohne Vorbildung in Klasse 10 ist eine Überprüfung/Feststellungsprüfung erforderlich). BF Ethik bleibt grundsätzlich wählbar.", ["ER", "KR", "ETH"]);
        if (p.SportExemptSemesters is not { } exemptions)
            Add("AGVO-SPORT", "Sportbefreiung und Ersatzbedarf sind unbekannt.", ["SP"]);
        else
        {
            var requiredSport = Enumerable.Range(1, 4).Except(exemptions).ToArray();
            if (requiredSport.Any(h => !choice.Selections.Any(s => s.SubjectId == "SP" && s.Semesters.Contains(h))))
                Add("AGVO-REQUIRED", "Sportbelegung in nicht befreiten Halbjahren fehlt.", ["SP"], requiredSport);
            if (exemptions.Length > 0)
            {
                Add("AGVO-SPORT", $"{exemptions.Length} Ersatzkurse nötig. Umfang/Einzelhalbjahre administrativ bestätigen; SIA ist kein Ersatz.", ["SP"], [.. exemptions]);
                var totalWithoutSia = choice.Selections.Where(s => s.Type != CourseType.Seminar).Sum(s => s.Semesters.Length);
                var extras = MaximumSportReplacementCourses(choice, profile);
                if (totalWithoutSia < 42 || extras < exemptions.Length)
                    Add("AGVO-SPORT-REPLACEMENT", $"{exemptions.Length} Ersatzkurse erforderlich (mindestens 42 Halbjahreskurse ohne SIA; aktuell {totalWithoutSia} ohne SIA, höchstens {extras} zusätzliche Basis-/Wahlfachkurse vorhanden). SIA ersetzt keine Sportkurse.", ["SP"], [.. exemptions]);
            }
        }
        if (lf.Any(s => s.SubjectId == "SP") && p.PermanentSportExemption != Knowledge.No)
            Add("AGVO-SPORT", "LF Sport: dauerhafte auch teilweise Befreiung bzw. fehlende Auskunft erfordert Klärung.", ["SP"]);
        if (lf.Length == 3 && ExamOptions(choice, catalog, profile).IsEmpty)
            Add("AGVO-EXAM", "Die belegten Kurse ergeben keine mögliche mündliche Zweierkombination für Deutsch, Mathematik und alle Aufgabenfelder.");
        Add("AGVO-CREDIT", "Belegungszahl und Pflichtanrechnung sind getrennt. Angezeigte Mindestanrechnung ist vorläufig; Sonderfälle/Schulversuch und BLL sind zu bestätigen.");
        Add("AGVO-ELECTION", "Vorwahl: keine verbindliche Abgabe. Endwahltermin und Unterrichtsende der Einführungsphase sind noch nicht konfiguriert.");
        var summaries = ChoiceValidator.Summarize(choice, catalog);
        if (summaries.Average(s => s.WeeklyHours) < 32)
            Add("PLAN-HOURS", $"Durchschnitt {summaries.Average(s => s.WeeklyHours):0.##} Wochenstunden; Planungsrichtwert 32, kein automatischer Rechtsverstoß.");
        return messages.ToImmutable();
    }

    public static int MaximumSportReplacementCourses(CourseChoice choice, ProfileContext profile)
    {
        var available = choice.Selections.Where(s => s.Type is CourseType.Bf or CourseType.Elective && s.SubjectId != "SP")
            .SelectMany(s => s.Semesters.Select(h => (s.SubjectId, h))).ToHashSet();
        var mandatory = ChoiceWorkflow.Required(choice, profile).Where(r => r.SubjectId != "SP")
            .SelectMany(r => r.Semesters.Select(h => (r.SubjectId, h))).ToHashSet();
        var full = choice.Selections.Where(Four).Select(s => s.SubjectId).ToArray();
        var arts = full.Intersect(["BK", "MU"]).DefaultIfEmpty("BK");
        var religions = full.Intersect(["ETH", "ER", "KR"]).DefaultIfEmpty("ETH");
        var languages = full.Intersect(Languages(profile)).DefaultIfEmpty("E");
        var sciences = full.Intersect(Sciences).DefaultIfEmpty("BIO");
        var max = 0;
        foreach (var art in arts)
        foreach (var religion in religions)
        foreach (var language in languages)
        foreach (var science in sciences)
        {
            var seconds = full.Where(s => (Languages(profile).Contains(s) && s != language)
                || (Sciences.Contains(s) && s != science)
                || (s == "INF" && profile.Status == ProfileStatus.Verified
                    && (profile.Prerequisites.InformaticsBridge == Knowledge.Yes || profile.Prerequisites.InformaticsImp == Knowledge.Yes))).DefaultIfEmpty("CH");
            foreach (var second in seconds)
            {
                var reserved = new HashSet<(string, int)>(mandatory);
                foreach (var s in new[] { art, religion, language, science, second })
                    foreach (var h in Enumerable.Range(1, 4)) reserved.Add((s, h));
                max = Math.Max(max, available.Except(reserved).Count());
            }
        }
        return max;
    }

    public static ImmutableArray<ExamOption> ExamOptions(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        var lf = choice.Selections.Where(s => s.Type == CourseType.Lf).Select(s => s.SubjectId).ToArray();
        if (lf.Length != 3) return [];
        var candidates = ChoiceWorkflow.OralCandidates(choice, catalog, profile)
            .Where(id => choice.Selections.Any(s => s.SubjectId == id && ChoiceWorkflow.OralSemesters(choice, id).All(s.Semesters.Contains))).Order().ToArray();
        var options = ImmutableArray.CreateBuilder<ExamOption>();
        for (var i = 0; i < candidates.Length; i++)
        for (var j = i + 1; j < candidates.Length; j++)
        {
            var all = lf.Concat([candidates[i], candidates[j]]).ToArray();
            if (!all.Contains("D") || !all.Contains("M") || !new[] { 1, 2, 3 }.All(f => all.Any(s => Field(s, profile) == f))) continue;
            var floor = MinimumCredits(choice, [candidates[i], candidates[j]], profile);
            if (floor > 40) continue;
            var note = "Vorläufig; Rechtsstand, persönliche Prüfungsvoraussetzungen und Anrechnung bestätigen. BLL/SIA ersetzt kein Fach automatisch.";
            options.Add(new(candidates[i], candidates[j], floor, ReviewStatus.NeedsReview, note));
        }
        return options.ToImmutable();
    }

    // Distinct subject/semester sets avoid counting LF twice. Missing required courses are not hidden.
    public static int MinimumCredits(CourseChoice choice, string[] oral, ProfileContext profile)
    {
        var required = new HashSet<(string, int)>();
        void Add(string s, IEnumerable<int> h) { foreach (var semester in h) required.Add((s, semester)); }
        int[] four = [1, 2, 3, 4];
        foreach (var s in choice.Selections.Where(s => s.Type == CourseType.Lf)) Add(s.SubjectId, four);
        foreach (var r in ChoiceWorkflow.Required(choice with { PlannedOralSubjects = [] }, profile).Where(r => r.SubjectId is "D" or "M" or "G" or "GK" or "GEO")) Add(r.SubjectId, r.Semesters);
        foreach (var s in oral) Add(s, ChoiceWorkflow.OralSemesters(choice, s));
        var art = choice.Selections.Where(s => s.SubjectId is "BK" or "MU").OrderByDescending(s => s.Type == CourseType.Lf || oral.Contains(s.SubjectId)).FirstOrDefault();
        if (art is not null) Add(art.SubjectId, art.Semesters.Take(2));
        var languages = choice.Selections.Where(s => Four(s) && Languages(profile).Contains(s.SubjectId)).Select(s => s.SubjectId).Distinct().DefaultIfEmpty("__LANG1").ToArray();
        var sciences = choice.Selections.Where(s => Four(s) && Sciences.Contains(s.SubjectId)).Select(s => s.SubjectId).Distinct().DefaultIfEmpty("__SCI1").ToArray();
        var minima = new List<int>();
        foreach (var language in languages)
        foreach (var science in sciences)
        {
            foreach (var second in languages.Where(s => s != language && s != "__LANG1").Concat(sciences.Where(s => s != science && s != "__SCI1"))
                .Concat(choice.Selections.Where(s => s.SubjectId == "INF" && Four(s) && profile.Status == ProfileStatus.Verified
                    && (profile.Prerequisites.InformaticsImp == Knowledge.Yes || profile.Prerequisites.InformaticsBridge == Knowledge.Yes)).Select(s => s.SubjectId))
                .DefaultIfEmpty("__BREADTH2"))
            {
                var set = new HashSet<(string, int)>(required);
                foreach (var s in new[] { language, science, second }) foreach (var h in four) set.Add((s, h));
                minima.Add(set.Count);
            }
        }
        return minima.Min();
    }
}

