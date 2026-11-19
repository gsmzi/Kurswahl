using System.Text.RegularExpressions;
using GsmKurswahl.Domain;

namespace GsmKurswahl.ImportExport;

/// <summary>Pure presentation helpers translating technical rule/subject codes into clear German for printable documents.</summary>
internal static class PlainDocumentText
{
    private static readonly Regex LeadingRuleId = new(@"^(?:(?:AGVO|PLAN|GSM|CATALOG|PROFILE|OFFER|SUBJECT|PREREQUISITE)-[A-Z0-9-]+:\s*)+", RegexOptions.Compiled);
    private static readonly Regex PlanHoursPattern = new(@"Durchschnitt\s+([0-9.,]+)\s+Wochenstunden", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> FallbackSubjectNames = new(StringComparer.Ordinal)
    {
        ["D"] = "Deutsch",
        ["M"] = "Mathematik",
        ["E"] = "Englisch",
        ["F"] = "Französisch",
        ["L"] = "Latein",
        ["I"] = "Italienisch",
        ["BIO"] = "Biologie",
        ["CH"] = "Chemie",
        ["PH"] = "Physik",
        ["G"] = "Geschichte",
        ["GK"] = "Gemeinschaftskunde",
        ["GEO"] = "Geographie",
        ["W"] = "Wirtschaft",
        ["BK"] = "Bildende Kunst",
        ["MU"] = "Musik",
        ["ETH"] = "Ethik",
        ["ER"] = "Evangelische Religion",
        ["KR"] = "Katholische Religion",
        ["SP"] = "Sport",
        ["INF"] = "Informatik",
        ["PSY"] = "Psychologie",
        ["SIA"] = "Schüler-Ingenieur-Akademie (SIA)"
    };

    public static string SubjectName(OfferCatalog catalog, string subjectId) =>
        catalog.Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name
        ?? (FallbackSubjectNames.TryGetValue(subjectId, out var fallback) ? fallback : subjectId);

    public static string FormatCourseTypeFull(CourseType type) => type switch
    {
        CourseType.Lf => "Leistungsfach",
        CourseType.Bf => "Basisfach",
        CourseType.Elective => "Wahlfach",
        _ => "Seminarkurs"
    };

    public static string FormatSemesters(IEnumerable<int> semesters)
    {
        var ordered = semesters.Distinct().Order().ToArray();
        if (ordered.Length == 0) return "";
        if (ordered.Length == 1) return "HJ " + ordered[0];
        if (ordered.Length == 2 && ordered[1] == ordered[0] + 1) return $"HJ {ordered[0]} und {ordered[1]}";
        if (ordered.Length >= 3 && ordered[^1] - ordered[0] == ordered.Length - 1) return $"HJ {ordered[0]}–{ordered[^1]}";
        return "HJ " + string.Join(", ", ordered);
    }

    public static string FormatOfferFull(CourseOffer offer, OfferCatalog catalog)
    {
        var variant = offer.Variant == CourseVariant.Bilingual ? " bilingual" : "";
        var semesters = FormatSemesters(offer.Schedule.Select(s => s.Semester));
        return $"{SubjectName(catalog, offer.SubjectId)} ({FormatCourseTypeFull(offer.Type)}{variant}, {semesters}){CourseAnnotations.Suffix(offer)}";
    }

    public static bool ShouldSuppressInDocument(RuleMessage message, CourseChoice? choice)
    {
        if (message.Rule.Id == "PROFILE-TRUST") return true;
        if (choice is not null && message.Rule.Id == "AGVO-COURSES")
        {
            var lfCourses = choice.Selections.Where(s => s.Type == CourseType.Lf).Sum(s => s.Semesters.Length);
            var otherCourses = choice.Selections.Where(s => s.Type != CourseType.Lf).Sum(s => s.Semesters.Length);
            if (lfCourses < 12 && otherCourses >= 30) return true;
        }
        return false;
    }

    public static string FormatMessage(RuleMessage message, OfferCatalog catalog, CourseChoice? choice = null) => message.Rule.Id switch
    {
        "AGVO-LF" => FormatLfMessage(catalog, choice),
        "AGVO-COURSES" => FormatCourseCountMessage(choice),
        "AGVO-REQUIRED" => FormatRequiredMessage(message, catalog),
        "AGVO-LANGUAGE-SCIENCE" => "Sprachen und Naturwissenschaften: Erforderlich sind mindestens eine fortgeführte Fremdsprache und eine Naturwissenschaft (Biologie, Chemie oder Physik) über HJ 1–4 sowie zusätzlich eine zweite fortgeführte Fremdsprache oder eine zweite Naturwissenschaft (bei entsprechender Vorbildung kann Informatik die zweite Naturwissenschaft ersetzen).",
        "AGVO-SEMESTERS" => FormatSemestersMessage(message, catalog),
        "GSM-GK-CONTINUITY" => "Gemeinschaftskunde: Bei Leistungsfach Wirtschaft darf Gemeinschaftskunde nach einer Pause in HJ 2 nicht in HJ 3 und 4 weiterbelegt werden. Für Gemeinschaftskunde in HJ 3 und 4 bitte auch HJ 2 wählen.",
        "GSM-RELIGION-EXCLUSIVE" => "Religion oder Ethik: Ethik und Religion sowie verschiedene Religionsfächer dürfen nicht gleichzeitig belegt werden. Bitte nur ein Fach wählen.",
        "GSM-RELIGION-CONFESSION" => "Religionswahl: Evangelische bzw. Katholische Religion ist nur passend zum in Klasse 10 besuchten Fach wählbar (Ethik bleibt wählbar). Bei Fragen bitte an die Oberstufenberatung wenden.",
        "PLAN-ORAL" => FormatOralMessage(message.Explanation, catalog),
        "AGVO-EXAM" => "Prüfungsfächer: Aus den aktuell belegten Kursen ergibt sich noch keine zulässige Kombination aus drei Leistungsfächern und zwei mündlichen Prüfungsfächern für Deutsch, Mathematik und alle drei Aufgabenfelder.",
        "PLAN-HOURS" => FormatHoursMessage(message.Explanation, catalog),
        _ => Sanitize(message.Explanation, catalog)
    };

    public static string Sanitize(string text, OfferCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var value = LeadingRuleId.Replace(text.Trim(), "");
        foreach (var subject in catalog.Subjects.OrderByDescending(x => x.Id.Length))
        {
            if (value.StartsWith(subject.Id + ": ", StringComparison.Ordinal))
            {
                value = subject.Name + ": " + value[(subject.Id.Length + 2)..];
                break;
            }
            if (value.StartsWith(subject.Id + " ", StringComparison.Ordinal))
            {
                value = subject.Name + value[subject.Id.Length..];
                break;
            }
        }
        value = value
            .Replace("BIO/CH/PH", "Biologie, Chemie oder Physik", StringComparison.Ordinal)
            .Replace("BK / MU", "Bildende Kunst oder Musik", StringComparison.Ordinal)
            .Replace("ETH / ER / KR", "Ethik, Evangelische Religion oder Katholische Religion", StringComparison.Ordinal)
            .Replace("ER / KR / ETH", "Evangelische Religion, Katholische Religion oder Ethik", StringComparison.Ordinal)
            .Replace("needs_review", "durch die Oberstufenberatung zu klären", StringComparison.Ordinal)
            .Replace("profile_invalid", "Profil ungültig", StringComparison.Ordinal)
            .Replace("Profilstatus: Missing", "persönliches Profil fehlt", StringComparison.Ordinal)
            .Replace("Profilstatus: ProfileInvalid", "persönliches Profil ungültig", StringComparison.Ordinal)
            .Replace("Profilstatus: Unverified", "persönliches Profil ungeprüft", StringComparison.Ordinal)
            .Replace("Profilstatus: Verified", "persönliches Profil geprüft", StringComparison.Ordinal)
            .Replace(" -> ", " → ", StringComparison.Ordinal);

        foreach (var id in new[] { "ETH", "ER", "KR", "GEO", "GK", "INF" })
        {
            var fullName = SubjectName(catalog, id);
            value = Regex.Replace(value, $@"(?<![A-Za-zÄÖÜäöüß]){Regex.Escape(id)}(?![A-Za-zÄÖÜäöüß])", fullName);
        }
        value = Regex.Replace(value, @"(?<!Schüler-Ingenieur-Akademie \()(?<![A-Za-zÄÖÜäöüß])SIA(?![A-Za-zÄÖÜäöüß])(?!\))", "Schüler-Ingenieur-Akademie (SIA)");
        value = value
            .Replace("LF-Halbjahreskurse", "Leistungsfach-Halbjahreskurse", StringComparison.Ordinal)
            .Replace("LF-Belegung", "Leistungsfach-Belegung", StringComparison.Ordinal)
            .Replace("LF-Eignung", "Leistungsfach-Eignung", StringComparison.Ordinal)
            .Replace("INF-Alternative", "Informatik-Alternative", StringComparison.Ordinal)
            .Replace("GK-Kurse", "Gemeinschaftskunde-Kurse", StringComparison.Ordinal);
        value = Regex.Replace(value, @"(?<![A-Za-zÄÖÜäöüß-])LF(?![A-Za-zÄÖÜäöüß-])", "Leistungsfach");
        value = Regex.Replace(value, @"(?<![A-Za-zÄÖÜäöüß-])BF(?![A-Za-zÄÖÜäöüß-])", "Basisfach");
        return value;
    }

    private static string FormatLfMessage(OfferCatalog catalog, CourseChoice? choice)
    {
        if (choice is not null)
        {
            var lfCount = choice.Selections.Where(s => s.Type == CourseType.Lf).Select(s => s.SubjectId).Distinct().Count();
            if (lfCount < 3)
            {
                var missing = 3 - lfCount;
                return (missing == 1 ? "Es fehlt noch 1 Leistungsfach." : $"Es fehlen noch {missing} Leistungsfächer.")
                    + " Insgesamt werden drei verschiedene Leistungsfächer über alle vier Halbjahre benötigt, davon mindestens zwei aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik).";
            }
            return "Leistungsfächer: Mindestens zwei der drei Leistungsfächer müssen aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik) sein.";
        }
        return "Leistungsfächer: Benötigt werden genau drei verschiedene Leistungsfächer über alle vier Halbjahre, davon mindestens zwei aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik).";
    }

    private static string FormatCourseCountMessage(CourseChoice? choice)
    {
        if (choice is not null)
        {
            var otherCourses = choice.Selections.Where(s => s.Type != CourseType.Lf).Sum(s => s.Semesters.Length);
            if (otherCourses < 30)
            {
                var missing = 30 - otherCourses;
                return $"Kursanzahl: Es {(missing == 1 ? "fehlt noch 1 weiterer Halbjahreskurs" : $"fehlen noch {missing} weitere Halbjahreskurse")} bis zur Mindestzahl von 42 Kursen (12 Leistungsfachkurse und mindestens 30 weitere Kurse).";
            }
        }
        return "Kursanzahl: Insgesamt werden 12 Leistungsfachkurse und mindestens 30 weitere Halbjahreskurse (zusammen mindestens 42 Kurse) benötigt. Die Schüler-Ingenieur-Akademie (SIA) zählt dabei mit ihren zwei Halbjahren.";
    }

    private static string FormatRequiredMessage(RuleMessage message, OfferCatalog catalog)
    {
        if (message.SubjectIds.Any(s => s is "BK" or "MU"))
            return "Bildende Kunst oder Musik: Es fehlen durchgehend vier Halbjahreskurse (HJ 1–4) in einem der beiden Fächer.";
        if (message.SubjectIds.Any(s => s is "ETH" or "ER" or "KR"))
            return "Religion oder Ethik: In allen vier Halbjahren (HJ 1–4) muss Religion oder Ethik belegt sein.";
        if (message.SubjectIds.Length == 1)
        {
            var subject = message.SubjectIds[0];
            var semesters = message.Semesters.IsEmpty ? "HJ 1–4" : FormatSemesters(message.Semesters);
            return $"{SubjectName(catalog, subject)}: Pflichtbelegung fehlt ({semesters}).";
        }
        return Sanitize(message.Explanation, catalog);
    }

    private static string FormatSemestersMessage(RuleMessage message, OfferCatalog catalog)
    {
        var subject = message.SubjectIds.FirstOrDefault();
        var prefix = subject is null ? "" : SubjectName(catalog, subject) + ": ";
        var extra = subject is "GK" or "GEO"
            ? " Bei Leistungsfach Wirtschaft sind Gemeinschaftskunde in HJ 1/2 und Geographie in HJ 3/4 einzeln wählbar; zusätzliche Kurse in Gemeinschaftskunde in HJ 3 und 4 bleiben zusammen."
            : "";
        return prefix + "Kurse werden in der Regel für ganze Schuljahre (HJ 1 und 2 bzw. HJ 3 und 4) gewählt." + extra;
    }

    private static string FormatOralMessage(string explanation, OfferCatalog catalog)
    {
        if (explanation.Contains("Aufgabenfelder", StringComparison.Ordinal))
            return "Mündliche Prüfungsfächer: Zusammen mit den drei Leistungsfächern und zwei mündlichen Prüfungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sein.";
        if (explanation.Contains("Zwei verschiedene", StringComparison.Ordinal))
            return "Mündliche Prüfungsfächer: Es müssen zwei verschiedene Basisfächer als mündliche Prüfungsfächer geplant werden.";
        if (explanation.Contains("passt nicht mehr", StringComparison.Ordinal))
            return "Mündliche Prüfungsfächer: Die bisherige mündliche Prüfungsplanung passt nicht mehr zu den gewählten Kursen und muss neu festgelegt werden.";
        return Sanitize(explanation, catalog);
    }

    private static string FormatHoursMessage(string explanation, OfferCatalog catalog)
    {
        var match = PlanHoursPattern.Match(explanation);
        return match.Success
            ? $"Wochenstunden: Durchschnittlich {match.Groups[1].Value} Wochenstunden pro Halbjahr (Planungsrichtwert sind 32 Wochenstunden; dies ist ein Hinweis, kein automatischer Fehler)."
            : Sanitize(explanation, catalog);
    }
}
