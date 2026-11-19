using System.Collections.Immutable;
using System.Text.RegularExpressions;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed record GuidanceStep(string Heading, string Text, string? Options = null);
public sealed record ReselectionGuidance(ImmutableArray<GuidanceStep> Steps, string? SummaryNote);
public sealed record ControlSheetGuidance(ImmutableArray<string> Violations, ImmutableArray<string> Hints);

/// <summary>Translates rule validation results into structured, jargon-free guidance for printable sheets.</summary>
public static class DocumentGuidance
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

    public static ReselectionGuidance BuildReselection(StudentRecord student, CourseChoice remaining, OfferCatalog catalog, IEnumerable<string> removedOfferIds)
    {
        var profile = new ProfileContext(ProfileStatus.Verified, student.Student.Prerequisites);
        var validator = new ChoiceValidator();
        var validation = validator.Validate(remaining with { OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version }, catalog, profile);
        var removedSet = removedOfferIds.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var removedOffers = catalog.Offers.Where(o => removedSet.Contains(o.Id)).ToArray();

        var steps = new List<GuidanceStep>();
        var sciences = AcademicRules.ScienceSubjects(catalog);
        var languages = AcademicRules.Languages(profile, catalog);
        bool IsCore(string subjectId) => subjectId is "D" or "M" || sciences.Contains(subjectId) || languages.Contains(subjectId);

        var lf = remaining.Selections.Where(s => s.Type == CourseType.Lf).ToArray();
        var coreCount = lf.Count(s => IsCore(s.SubjectId));
        var missingLf = Math.Max(0, 3 - lf.Length);

        if (missingLf > 0)
        {
            var needCore = Math.Max(0, 2 - coreCount);
            var onlyCoreAllowed = needCore >= missingLf;
            var selectableLfs = catalog.Offers
                .Where(o => o.Type == CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established
                    && !lf.Any(s => s.SubjectId == o.SubjectId)
                    && (!onlyCoreAllowed || IsCore(o.SubjectId))
                    && validator.GetAvailability(o, profile).CanSelect)
                .OrderByDescending(o => removedOffers.Any(r => r.Type == CourseType.Lf && r.SubjectId == o.SubjectId))
                .ThenBy(o => SubjectName(catalog, o.SubjectId), StringComparer.CurrentCulture)
                .ThenBy(o => o.Variant)
                .Select(o => FormatOfferLabel(o, catalog))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var heading = missingLf == 1 ? "Neues Leistungsfach (HJ 1–4)" : "Neue Leistungsfächer (HJ 1–4)";
            var text = onlyCoreAllowed
                ? $"Du brauchst {(missingLf == 1 ? "1 neues Leistungsfach" : $"{missingLf} neue Leistungsfächer")} aus den Kernfächern (Deutsch, Mathematik, deinen fortgeführten Fremdsprachen oder einer Naturwissenschaft), da mindestens zwei Leistungsfächer Kernfächer sein müssen."
                : needCore > 0
                    ? $"Du brauchst {missingLf} neue Leistungsfächer über alle vier Halbjahre, darunter mindestens {needCore} Kernfach (Deutsch, Mathematik, fortgeführte Fremdsprache oder Naturwissenschaft)."
                    : $"Du brauchst {(missingLf == 1 ? "1 neues Leistungsfach" : $"{missingLf} neue Leistungsfächer")} über alle vier Halbjahre.";
            var options = selectableLfs.Length == 0
                ? null
                : $"Wählbare Leistungsfächer{(onlyCoreAllowed ? " (Kernfächer)" : "")}: {string.Join(", ", selectableLfs)}. Hinweis: Wählst du ein bisheriges Basisfach als Leistungsfach, entfällt dort das Basisfach.";
            steps.Add(new(heading, text, options));
        }
        else if (coreCount < 2)
        {
            steps.Add(new("Leistungsfächer anpassen",
                "Mindestens zwei der drei Leistungsfächer müssen aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder einer Naturwissenschaft (Biologie, Chemie oder Physik) sein."));
        }

        var baseRequiredSubjects = ChoiceWorkflow.Required(remaining with { PlannedOralSubjects = [] }, profile)
            .Select(r => r.SubjectId).ToHashSet(StringComparer.Ordinal);
        var missingRequiredSemesters = 0;
        foreach (var req in ChoiceWorkflow.Required(remaining, profile))
        {
            var existingSems = remaining.Selections.Where(s => s.SubjectId == req.SubjectId).SelectMany(s => s.Semesters).ToHashSet();
            var missingSems = req.Semesters.Where(h => !existingSems.Contains(h)).ToArray();
            if (missingSems.Length == 0) continue;
            if (!baseRequiredSubjects.Contains(req.SubjectId) && removedOffers.Any(o => o.SubjectId == req.SubjectId))
                continue;

            missingRequiredSemesters += missingSems.Length;
            var name = SubjectName(catalog, req.SubjectId);
            var semText = FormatSemesters(missingSems);
            var text = removedOffers.Any(o => o.SubjectId == req.SubjectId && o.Type == CourseType.Lf)
                ? $"Da {name} nicht mehr dein Leistungsfach ist, muss {name} als Basisfach in {semText} belegt werden."
                : $"{name} muss in {semText} als Basisfach belegt werden.";
            steps.Add(new($"Pflichtfach {name} ({semText})", text));
        }

        var missingTaskSemesters = 0;
        foreach (var task in ChoiceWorkflow.Tasks(remaining, catalog, profile).Where(t => !t.IsSatisfied))
        {
            missingTaskSemesters += task.Open * task.Semesters.Length;
            var optionNames = task.OptionOfferIds
                .Select(id => catalog.Offers.FirstOrDefault(o => o.Id == id))
                .OfType<CourseOffer>()
                .OrderBy(o => SubjectName(catalog, o.SubjectId), StringComparer.CurrentCulture)
                .ThenBy(o => o.Variant)
                .Select(o => FormatOfferLabel(o, catalog))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var optionsText = optionNames.Length == 0 ? null : "Mögliche Basisfächer: " + string.Join(", ", optionNames) + ".";
            var (heading, text) = DescribeTask(task);
            steps.Add(new(heading, text, optionsText));
        }

        if (validation.Messages.Any(m => m.Rule.Id is "PLAN-ORAL" or "AGVO-EXAM" && m.Rule.Severity == ReviewStatus.Invalid))
        {
            steps.Add(BuildExamStep(remaining, catalog, profile, validator, lf, missingLf));
        }

        var otherCourses = remaining.Selections.Where(s => s.Type != CourseType.Lf).Sum(s => s.Semesters.Length);
        var projectedOther = otherCourses + missingRequiredSemesters + missingTaskSemesters;
        if (projectedOther < 30)
        {
            var missingExtra = 30 - projectedOther;
            var extras = catalog.Offers
                .Where(o => o.Type != CourseType.Lf && o.Status is OfferStatus.Offered or OfferStatus.Established
                    && !remaining.Selections.Any(s => s.SubjectId == o.SubjectId || AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, o.SubjectId))
                    && validator.GetAvailability(o, profile).CanSelect)
                .OrderBy(o => SubjectName(catalog, o.SubjectId), StringComparer.CurrentCulture)
                .ThenBy(o => o.Type)
                .ThenBy(o => o.Variant)
                .Select(o => FormatOfferLabelWithType(o, catalog))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var options = extras.Length == 0 ? null : "Mögliche weitere Kurse: " + string.Join(", ", extras) + ".";
            steps.Add(new("Zusätzliche Kurse (Mindestzahl 42)",
                $"Es {(missingExtra == 1 ? "fehlt noch 1 weiterer Halbjahreskurs" : $"fehlen noch {missingExtra} weitere Halbjahreskurse")}, um insgesamt mindestens 42 Kurse (12 Leistungsfachkurse und mindestens 30 weitere Kurse) zu erreichen. Zusätzliche Kurse werden in der Regel für ein ganzes Schuljahr (HJ 1 und 2 bzw. HJ 3 und 4) gewählt.",
                options));
        }

        foreach (var message in validation.Messages.Where(m => m.Rule.Severity == ReviewStatus.Invalid))
        {
            if (message.Rule.Id is "AGVO-LF" or "AGVO-COURSES" or "AGVO-REQUIRED" or "AGVO-LANGUAGE-SCIENCE" or "PLAN-ORAL" or "AGVO-EXAM")
                continue;
            steps.Add(BuildRemainingRuleStep(message, remaining, catalog, profile, validator));
        }

        string? summaryNote = null;
        if (steps.Count == 0)
        {
            summaryNote = removedOffers.Length > 0
                ? "Deine verbleibende Kurswahl erfüllt weiterhin alle Pflichtbedingungen. Eine Nachwahl ist nur nötig, wenn du stattdessen freiwillig ein anderes Fach wählen möchtest."
                : "Aktuell liegen keine Regelverstöße vor. Falls du dennoch eine Änderung wünschst, trage sie unten ein.";
        }

        return new([.. steps], summaryNote);
    }

    public static ControlSheetGuidance FormatControlSheetMessages(ValidationResult? validation, OfferCatalog catalog, CourseChoice? choice = null)
    {
        if (validation is null) return new([], []);
        var hasPlanOralInvalid = validation.Messages.Any(m => m.Rule.Id == "PLAN-ORAL" && m.Rule.Severity == ReviewStatus.Invalid);
        var violations = new List<string>();
        var hints = new List<string>();
        foreach (var message in validation.Messages)
        {
            if (ShouldSuppress(message, choice)) continue;
            if (message.Rule.Id == "AGVO-EXAM" && hasPlanOralInvalid) continue;
            var formatted = FormatRuleMessage(message, catalog, choice);
            if (string.IsNullOrWhiteSpace(formatted)) continue;
            var target = message.Rule.Severity == ReviewStatus.Invalid ? violations : hints;
            if (!target.Contains(formatted, StringComparer.Ordinal))
                target.Add(formatted);
        }
        return new([.. violations], [.. hints]);
    }

    private static (string Heading, string Text) DescribeTask(ChoiceTask task) => task.Id switch
    {
        "PLAN-AREA-ART" => ("Kunst oder Musik (HJ 1–4)", "Du brauchst durchgehend vier Halbjahreskurse in Bildende Kunst oder Musik."),
        "PLAN-AREA-RELIGION" => ("Religion oder Ethik (HJ 1–4)", "Du brauchst durchgehend vier Halbjahreskurse in Religion oder Ethik."),
        "PLAN-AREA-LANGUAGE" => ("Fortgeführte Fremdsprache (HJ 1–4)", "Du brauchst mindestens eine fortgeführte Fremdsprache über alle vier Halbjahre (als Leistungsfach oder Basisfach)."),
        "PLAN-AREA-SCIENCE" => ("Naturwissenschaft (HJ 1–4)", "Du brauchst mindestens eine Naturwissenschaft über alle vier Halbjahre (als Leistungsfach oder Basisfach)."),
        "PLAN-AREA-BREADTH" => ("Zweite Fremdsprache oder zweite Naturwissenschaft (HJ 1–4)", "Du brauchst zusätzlich eine zweite fortgeführte Fremdsprache oder eine zweite Naturwissenschaft über alle vier Halbjahre (falls nicht schon durch ein neues Leistungsfach abgedeckt)."),
        _ => (task.Title, "Bitte wähle ausdrücklich die normale oder bilinguale Kursvariante.")
    };

    private static GuidanceStep BuildExamStep(CourseChoice remaining, OfferCatalog catalog, ProfileContext profile,
        ChoiceValidator validator, CourseSelection[] lf, int missingLf)
    {
        var oralCandidates = ChoiceWorkflow.OralCandidates(remaining, catalog, profile);
        var validPlannedOral = remaining.PlannedOralSubjects.Where(oralCandidates.Contains).Distinct().ToArray();
        var examSubjects = lf.Select(s => s.SubjectId).Concat(validPlannedOral).Distinct().ToArray();

        var missingParts = new List<string>();
        if (!examSubjects.Contains("D")) missingParts.Add("Deutsch");
        if (!examSubjects.Contains("M")) missingParts.Add("Mathematik");

        var missingFields = new List<(int Field, string Label)>();
        if (!examSubjects.Any(s => AcademicRules.Field(s, profile, catalog) == 1))
            missingFields.Add((1, "sprachlich-literarisch-künstlerisches Aufgabenfeld"));
        if (!examSubjects.Any(s => AcademicRules.Field(s, profile, catalog) == 2))
            missingFields.Add((2, "gesellschaftswissenschaftliches Aufgabenfeld"));
        if (!examSubjects.Any(s => AcademicRules.Field(s, profile, catalog) == 3))
            missingFields.Add((3, "mathematisch-naturwissenschaftliches Aufgabenfeld"));

        string? FieldOptions(int field)
        {
            var subjects = catalog.Subjects
                .Where(s => AcademicRules.Field(s.Id, profile, catalog) == field
                    && !lf.Any(x => x.SubjectId == s.Id)
                    && catalog.Offers.Any(o => o.SubjectId == s.Id
                        && o.Status is OfferStatus.Offered or OfferStatus.Established
                        && (o.Type == CourseType.Lf || oralCandidates.Contains(o.SubjectId))
                        && validator.GetAvailability(o, profile).CanSelect))
                .OrderBy(s => s.Name, StringComparer.CurrentCulture)
                .Select(s => s.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return subjects.Length == 0 ? null : $"z. B. {string.Join(", ", subjects)}";
        }

        if (missingLf > 0)
        {
            var detailParts = new List<string>();
            if (missingParts.Count > 0) detailParts.Add(string.Join(" und ", missingParts));
            foreach (var (field, label) in missingFields)
            {
                var examples = FieldOptions(field);
                detailParts.Add(examples is null ? label : $"{label} (mögliche Fächer {examples})");
            }
            if (validPlannedOral.Length < 2)
                detailParts.Add($"zwei gültige mündliche Prüfungsfächer (mögliche Basisfächer: {string.Join(", ", oralCandidates.Select(id => SubjectName(catalog, id)).Order(StringComparer.CurrentCulture))})");

            return new("Prüfungsfächer (Abitur)",
                "Zusammen mit deinen drei Leistungsfächern und zwei mündlichen Prüfungsfächern müssen Deutsch, Mathematik und alle drei Aufgabenfelder (sprachlich-literarisch-künstlerisch, gesellschaftswissenschaftlich, mathematisch-naturwissenschaftlich) abgedeckt sein.",
                detailParts.Count == 0 ? null : "Aktuell noch nicht abgedeckt: " + string.Join("; ", detailParts) + ".");
        }

        var examCombos = AcademicRules.ExamOptions(remaining, catalog, profile);
        string? options;
        if (examCombos.Length > 0)
        {
            var formattedCombos = examCombos.Take(6)
                .Select(c => $"{SubjectName(catalog, c.First)} + {SubjectName(catalog, c.Second)}")
                .ToArray();
            options = "Mögliche Kombinationen aus deiner Belegung: " + string.Join(", ", formattedCombos) + (examCombos.Length > 6 ? " …" : "") + ".";
        }
        else
        {
            var useful = ChoiceWorkflow.UsefulOralCandidates(remaining with { PlannedOralSubjects = [] }, catalog, profile);
            var candidateIds = useful.Length > 0 ? useful : oralCandidates;
            var candidateNames = candidateIds.Select(id => SubjectName(catalog, id)).Order(StringComparer.CurrentCulture).Distinct().ToArray();
            var missingHint = missingFields.Count > 0 || missingParts.Count > 0
                ? $"Noch nicht abgedeckt: {string.Join(", ", missingParts.Concat(missingFields.Select(f => f.Label)))}. "
                : "";
            options = candidateNames.Length == 0
                ? null
                : missingHint + $"Mögliche mündliche Prüfungsfächer (über HJ 1–4 zu belegen): {string.Join(", ", candidateNames)}.";
        }

        return new("Prüfungsfächer (Abitur)",
            "Plane zwei zulässige Basisfächer (über HJ 1–4) als mündliche Prüfungsfächer, sodass zusammen mit deinen drei Leistungsfächern Deutsch, Mathematik und alle drei Aufgabenfelder abgedeckt sind.",
            options);
    }

    private static GuidanceStep BuildRemainingRuleStep(RuleMessage message, CourseChoice remaining, OfferCatalog catalog,
        ProfileContext profile, ChoiceValidator validator)
    {
        if (message.Rule.Id == "AGVO-SPORT-REPLACEMENT")
        {
            var extras = catalog.Offers
                .Where(o => o.Type is CourseType.Bf or CourseType.Elective && o.SubjectId != "SP"
                    && o.Status is OfferStatus.Offered or OfferStatus.Established
                    && !remaining.Selections.Any(s => s.SubjectId == o.SubjectId || AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, o.SubjectId))
                    && validator.GetAvailability(o, profile).CanSelect)
                .OrderBy(o => SubjectName(catalog, o.SubjectId), StringComparer.CurrentCulture)
                .Select(o => FormatOfferLabelWithType(o, catalog))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var exemptions = profile.Prerequisites.SportExemptSemesters?.Length ?? 0;
            var text = exemptions > 0
                ? $"Wegen deiner Sportbefreiung in {exemptions} Halbjahren sind ebenso viele zusätzliche Basis- oder Wahlfachkurse als Ausgleich erforderlich (mindestens 42 Halbjahreskurse ohne die Schüler-Ingenieur-Akademie). Zusätzliche Kurse werden als ganze Schuljahre (HJ 1 und 2 bzw. HJ 3 und 4) gewählt."
                : FormatRuleMessage(message, catalog, remaining);
            return new("Sportersatzkurse", text, extras.Length == 0 ? null : "Mögliche Ersatzkurse: " + string.Join(", ", extras) + ".");
        }

        var heading = message.Rule.Id switch
        {
            "GSM-GK-CONTINUITY" => "Gemeinschaftskunde (HJ 2–4)",
            "AGVO-SEMESTERS" => "Schuljahresweise Belegung",
            "GSM-RELIGION-EXCLUSIVE" => "Religion oder Ethik",
            "GSM-RELIGION-CONFESSION" => "Religionswahl",
            _ => "Belegung prüfen"
        };
        return new(heading, FormatRuleMessage(message, catalog, remaining));
    }

    private static bool ShouldSuppress(RuleMessage message, CourseChoice? choice)
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

    private static string FormatRuleMessage(RuleMessage message, OfferCatalog catalog, CourseChoice? choice) => message.Rule.Id switch
    {
        "AGVO-LF" => FormatLfMessage(choice),
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

    private static string FormatLfMessage(CourseChoice? choice)
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

    private static string Sanitize(string text, OfferCatalog catalog)
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

    private static string SubjectName(OfferCatalog catalog, string subjectId) =>
        catalog.Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name
        ?? (FallbackSubjectNames.TryGetValue(subjectId, out var fallback) ? fallback : subjectId);

    private static string FormatOfferLabel(CourseOffer offer, OfferCatalog catalog) =>
        SubjectName(catalog, offer.SubjectId)
        + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : "")
        + CourseAnnotations.Suffix(offer);

    private static string FormatOfferLabelWithType(CourseOffer offer, OfferCatalog catalog)
    {
        var type = offer.Type switch
        {
            CourseType.Lf => "Leistungsfach",
            CourseType.Bf => "Basisfach",
            CourseType.Elective => "Wahlfach",
            _ => "Seminarkurs"
        };
        var sems = FormatSemesters(offer.Schedule.Select(s => s.Semester));
        return $"{SubjectName(catalog, offer.SubjectId)} ({type}{(offer.Variant == CourseVariant.Bilingual ? " bilingual" : "")}, {sems}){CourseAnnotations.Suffix(offer)}";
    }

    private static string FormatSemesters(IEnumerable<int> semesters)
    {
        var ordered = semesters.Distinct().Order().ToArray();
        if (ordered.Length == 0) return "";
        if (ordered.Length == 1) return "HJ " + ordered[0];
        if (ordered.Length == 2 && ordered[1] == ordered[0] + 1) return $"HJ {ordered[0]} und {ordered[1]}";
        if (ordered.Length >= 3 && ordered[^1] - ordered[0] == ordered.Length - 1) return $"HJ {ordered[0]}–{ordered[^1]}";
        return "HJ " + string.Join(", ", ordered);
    }
}
