using System.Text.Json;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application.Tests;

public sealed class DocumentGuidanceTests
{
    private static OfferCatalog LoadDemoCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json");
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "fachangebot-demo.json"));
        return JsonSerializer.Deserialize<OfferCatalog>(File.ReadAllBytes(path), DocumentJson.Options)!;
    }

    private static StudentRecord Student(PriorEducation prerequisites) => new(
        SchemaVersions.StudentRecord,
        new(Guid.NewGuid(), "demo-012", "Testperson012, Demo", "10a", 2029, DepartureStatus.Active, prerequisites),
        "", "", true, 1, "demo-beratung", DateTimeOffset.UtcNow, null);

    private static void AssertJargonFree(string text)
    {
        foreach (var token in new[] { "PLAN-", "AGVO-", "GSM-", "CATALOG-", "PROFILE-", "GEO:", "GK:", "BIO/CH/PH", "BK / MU", "ETH / ER / KR", "needs_review", "profile_invalid" })
            Assert.DoesNotContain(token, text, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelledGeographyLfProducesClearStepsAndAvoidsRedundantCourseCount()
    {
        var baseCatalog = LoadDemoCatalog();
        var catalog = baseCatalog with
        {
            Offers = [.. baseCatalog.Offers.Select(o => o.Id == "GEO-LF" ? o with { Status = OfferStatus.Cancelled } : o)]
        };
        var prereq = new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [])
        {
            ContinuedLanguages = ["E", "F"],
            ReligionEthicsPermission = Knowledge.Yes,
            PermanentSportExemption = Knowledge.No
        };
        var student = Student(prereq);
        // Remaining after GEO-LF cancellation: D-LF, M-LF (2 LFs = 8 courses) + 32 other courses (>= 30).
        // Oral subjects: E + BIO -> Aufgabenfeld 2 (gesellschaftswissenschaftlich) is uncovered without GEO-LF.
        var remaining = new CourseChoice(2, Guid.NewGuid(), "demo-012", 2029, catalog.Version, ChoiceValidator.Version, 2,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary,
            [
                new("D-LF", "D", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("M-LF", "M", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("E-BF", "E", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("BIO-BF", "BIO", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("CH-BF", "CH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("G-BF", "G", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("GK-BF", "GK", CourseType.Bf, CourseVariant.Normal, [1, 2]),
                new("BK-BF", "BK", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("ETH-BF", "ETH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("SP-BF", "SP", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("PSY-WF", "PSY", CourseType.Elective, CourseVariant.Normal, [1, 2]),
                new("SIA-SK", "SIA", CourseType.Seminar, CourseVariant.Normal, [1, 2])
            ],
            ["E", "BIO"]);

        var guidance = DocumentGuidance.BuildReselection(student, remaining, catalog, ["GEO-LF"]);

        Assert.Null(guidance.SummaryNote);
        Assert.Contains(guidance.Steps, s => s.Heading == "Neues Leistungsfach (HJ 1–4)" && s.Options is not null && s.Options.Contains("Wählbare Leistungsfächer:"));
        Assert.Contains(guidance.Steps, s => s.Heading == "Pflichtfach Geographie (HJ 3 und 4)" && s.Text.Contains("nicht mehr dein Leistungsfach"));
        Assert.Contains(guidance.Steps, s => s.Heading == "Prüfungsfächer (Abitur)" && s.Options is not null && s.Options.Contains("gesellschaftswissenschaftliches Aufgabenfeld"));
        Assert.DoesNotContain(guidance.Steps, s => s.Heading.Contains("Mindestzahl 42"));
        foreach (var step in guidance.Steps)
        {
            AssertJargonFree(step.Heading);
            AssertJargonFree(step.Text);
            if (step.Options is not null) AssertJargonFree(step.Options);
        }
    }

    [Fact]
    public void CancelledBilingualBioLfWithOneCoreLfRequiresCoreLfAndExcludesRestrictedOptions()
    {
        var baseCatalog = LoadDemoCatalog();
        var catalog = baseCatalog with
        {
            Offers = [.. baseCatalog.Offers.Select(o => o.Id == "BIO-LF-BILI" ? o with { Status = OfferStatus.Cancelled } : o)]
        };
        var prereq = new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.Yes, "ETH", [])
        {
            ContinuedLanguages = ["E", "F"], // L, I, INF are NOT allowed for this student
            ReligionEthicsPermission = Knowledge.Yes,
            PermanentSportExemption = Knowledge.No
        };
        var student = Student(prereq);
        // Remaining LFs: M (core) + W (non-core). BIO-LF-BILI cancelled -> 1 core LF needed.
        // Also no other science chosen yet, so Naturwissenschaft (HJ 1-4) task is open.
        var remaining = new CourseChoice(2, Guid.NewGuid(), "demo-012", 2029, catalog.Version, ChoiceValidator.Version, 2,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary,
            [
                new("M-LF", "M", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("W-LF", "W", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("D-BF", "D", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("E-BF", "E", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("F-BF", "F", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("G-BF", "G", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("GK-BF", "GK", CourseType.Bf, CourseVariant.Normal, [1]),
                new("GEO-BF", "GEO", CourseType.Bf, CourseVariant.Normal, [3]),
                new("BK-BF", "BK", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("ETH-BF", "ETH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("SP-BF", "SP", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4])
            ],
            ["D", "E"]);

        var guidance = DocumentGuidance.BuildReselection(student, remaining, catalog, ["BIO-LF-BILI"]);

        var lfStep = Assert.Single(guidance.Steps, s => s.Heading == "Neues Leistungsfach (HJ 1–4)");
        Assert.Contains("Kernfächern", lfStep.Text);
        Assert.NotNull(lfStep.Options);
        Assert.Contains("Biologie", lfStep.Options);
        Assert.DoesNotContain("Latein", lfStep.Options);
        Assert.DoesNotContain("Italienisch", lfStep.Options);
        Assert.DoesNotContain("Geschichte", lfStep.Options);
        Assert.Contains(guidance.Steps, s => s.Heading == "Naturwissenschaft (HJ 1–4)");
        foreach (var step in guidance.Steps)
        {
            AssertJargonFree(step.Heading);
            AssertJargonFree(step.Text);
            if (step.Options is not null) AssertJargonFree(step.Options);
        }
    }

    [Fact]
    public void CancelledElectiveHandlesBothValidRemainingChoiceAndExactShortfall()
    {
        var baseCatalog = LoadDemoCatalog();
        var catalog = baseCatalog with
        {
            Offers = [.. baseCatalog.Offers.Select(o => o.Id == "PSY-WF" ? o with { Status = OfferStatus.Cancelled } : o)]
        };
        var prereq = new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [])
        {
            ContinuedLanguages = ["E", "F"],
            ReligionEthicsPermission = Knowledge.Yes,
            PermanentSportExemption = Knowledge.No
        };
        var student = Student(prereq);
        CourseSelection[] baseSelections =
        [
            new("D-LF", "D", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("M-LF", "M", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("E-LF", "E", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("BIO-BF", "BIO", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("CH-BF", "CH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("G-BF", "G", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("GK-BF", "GK", CourseType.Bf, CourseVariant.Normal, [1, 2]),
            new("GEO-BF", "GEO", CourseType.Bf, CourseVariant.Normal, [3, 4]),
            new("BK-BF", "BK", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("ETH-BF", "ETH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
            new("SP-BF", "SP", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4])
        ];
        // 3a: still 42 courses (includes F-BF)
        var validChoice = new CourseChoice(2, Guid.NewGuid(), "demo-012", 2029, catalog.Version, ChoiceValidator.Version, 2,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary,
            [.. baseSelections, new("F-BF", "F", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4])],
            ["BIO", "G"]);
        var stillValid = DocumentGuidance.BuildReselection(student, validChoice, catalog, ["PSY-WF"]);
        Assert.Empty(stillValid.Steps);
        Assert.NotNull(stillValid.SummaryNote);
        Assert.Contains("weiterhin alle Pflichtbedingungen", stillValid.SummaryNote);

        // 3b: drops to 40 courses (12 LF + 28 other)
        var shortChoice = validChoice with { Selections = [.. baseSelections] };
        var withShortfall = DocumentGuidance.BuildReselection(student, shortChoice, catalog, ["PSY-WF"]);
        var extraStep = Assert.Single(withShortfall.Steps);
        Assert.Equal("Zusätzliche Kurse (Mindestzahl 42)", extraStep.Heading);
        Assert.Contains("fehlen noch 2 weitere Halbjahreskurse", extraStep.Text);
        Assert.NotNull(extraStep.Options);
        AssertJargonFree(extraStep.Text);
        AssertJargonFree(extraStep.Options);
    }

    [Fact]
    public void ManualFollowUpWithMissingHistoryAvoidsRedundantExtraCourseStep()
    {
        var catalog = LoadDemoCatalog();
        var prereq = new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [])
        {
            ContinuedLanguages = ["E", "F"],
            ReligionEthicsPermission = Knowledge.Yes,
            PermanentSportExemption = Knowledge.No
        };
        var student = Student(prereq);
        // 12 LF + 26 other = 38, missing G (4 semesters) -> projectedOther = 30, so only Pfichtfach Geschichte should appear.
        var choice = new CourseChoice(2, Guid.NewGuid(), "demo-012", 2029, catalog.Version, ChoiceValidator.Version, 2,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, SubmissionStatus.Draft, ElectionPhase.Preliminary,
            [
                new("D-LF", "D", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("M-LF", "M", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("E-LF", "E", CourseType.Lf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("BIO-BF", "BIO", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("CH-BF", "CH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("GK-BF", "GK", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("GEO-BF", "GEO", CourseType.Bf, CourseVariant.Normal, [3, 4]),
                new("BK-BF", "BK", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("ETH-BF", "ETH", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4]),
                new("SP-BF", "SP", CourseType.Bf, CourseVariant.Normal, [1, 2, 3, 4])
            ],
            ["BIO", "GK"]);

        var guidance = DocumentGuidance.BuildReselection(student, choice, catalog, []);
        var step = Assert.Single(guidance.Steps);
        Assert.Equal("Pflichtfach Geschichte (HJ 1–4)", step.Heading);
        AssertJargonFree(step.Heading);
        AssertJargonFree(step.Text);
    }

    [Fact]
    public void FormatControlSheetMessagesStripsRuleIdsAndTechnicalTokens()
    {
        var catalog = LoadDemoCatalog();
        var ruleTrust = ChoiceValidator.Definitions.Single(d => d.Id == "PROFILE-TRUST");
        var ruleOral = ChoiceValidator.Definitions.Single(d => d.Id == "PLAN-ORAL");
        var ruleReq = ChoiceValidator.Definitions.Single(d => d.Id == "AGVO-REQUIRED");
        var ruleHours = ChoiceValidator.Definitions.Single(d => d.Id == "PLAN-HOURS");
        var ruleExam = ChoiceValidator.Definitions.Single(d => d.Id == "AGVO-EXAM");
        var validation = new ValidationResult(ReviewStatus.Invalid,
        [
            new(ruleTrust, [], [], "Kein verifiziertes persönliches Profil; Profilstatus: Missing", "Klärung"),
            new(ruleOral, [], [], "PLAN-ORAL: Deutsch, Mathematik und alle drei Aufgabenfelder müssen abgedeckt sein.", "Planen"),
            new(ruleExam, [], [], "Die belegten Kurse ergeben keine mögliche mündliche Zweierkombination für Deutsch, Mathematik und alle Aufgabenfelder.", "Planen"),
            new(ruleReq, ["GEO"], [3, 4], "GEO: Pflichtbelegung fehlt.", "Belegen"),
            new(ruleHours, [], [], "Durchschnitt 31 Wochenstunden; Planungsrichtwert 32, kein automatischer Rechtsverstoß.", "Prüfen")
        ]);

        var formatted = DocumentGuidance.FormatControlSheetMessages(validation, catalog);

        Assert.Equal(2, formatted.Violations.Length); // PLAN-ORAL + GEO; AGVO-EXAM deduplicated
        Assert.Single(formatted.Hints); // PROFILE-TRUST suppressed, PLAN-HOURS kept
        foreach (var text in formatted.Violations.Concat(formatted.Hints))
            AssertJargonFree(text);
        Assert.Contains(formatted.Violations, v => v.StartsWith("Geographie:", StringComparison.Ordinal));
        Assert.Contains(formatted.Hints, h => h.Contains("Planungsrichtwert sind 32 Wochenstunden", StringComparison.Ordinal));
    }
}
