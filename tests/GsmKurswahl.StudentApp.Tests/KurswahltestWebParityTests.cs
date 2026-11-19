using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Storage;
using GsmKurswahl.Tests;

namespace GsmKurswahl.StudentApp.Tests;

public class KurswahltestWebParityTests
{
    private static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "web", "Kurswahltest", "kurswahltest.js")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository-Wurzel mit web/Kurswahltest/kurswahltest.js nicht gefunden.");
    }

    [Fact]
    public void WebBundleIsCompleteAndMatchesDemoCatalog()
    {
        var repo = FindRepoRoot();
        var webDir = Path.Combine(repo, "web", "Kurswahltest");
        foreach (var file in new[] { "index.html", "styles.css", "kurswahltest.js", "catalog.js", "fachangebot.json", ".nojekyll" })
            Assert.True(File.Exists(Path.Combine(webDir, file)), $"Webdatei fehlt: {file}");

        var demoBytes = File.ReadAllBytes(Path.Combine(repo, "samples", "fachangebot-demo.json"));
        var webBytes = File.ReadAllBytes(Path.Combine(webDir, "fachangebot.json"));
        var demoCatalog = JsonDocuments.ReadCatalog(demoBytes);
        var webCatalog = JsonDocuments.ReadCatalog(webBytes);
        Assert.Equal(demoCatalog.Version, webCatalog.Version);
        Assert.Equal(demoCatalog.GraduationYear, webCatalog.GraduationYear);
        Assert.Equal(demoCatalog.Subjects.Length, webCatalog.Subjects.Length);
        Assert.Equal(demoCatalog.Offers.Length, webCatalog.Offers.Length);

        var catalogJs = File.ReadAllText(Path.Combine(webDir, "catalog.js"), Encoding.UTF8);
        Assert.Contains("window.GSM_DEFAULT_CATALOG", catalogJs);
        Assert.Contains(demoCatalog.Version, catalogJs);

        var appJs = File.ReadAllText(Path.Combine(webDir, "kurswahltest.js"), Encoding.UTF8);
        Assert.Contains("Wenn du möchtest, kannst du sie als unverbindlichen Kontrollbogen drucken oder als PDF speichern.", appJs);
        Assert.DoesNotContain("todo.length === 0 ? \"btn btn-primary\" : \"btn\"", appJs);
        Assert.DoesNotContain("displayText(`${m.rule.id}: ${m.explanation}`)", appJs);
        Assert.Contains("Noch zu korrigieren", appJs);
        Assert.Contains("Mit der Oberstufenberatung klären", appJs);
    }

    [Fact]
    public void BrowserEngineMatchesCSharpAcrossAllProfilesAndScenarios()
    {
        var repo = FindRepoRoot();
        var jsPath = Path.Combine(repo, "web", "Kurswahltest", "kurswahltest.js");
        var demoCatalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        var italianCatalog = demoCatalog with
        {
            Offers = [.. demoCatalog.Offers.Select(o => o.Id == "I-BF" ? o with { Status = OfferStatus.Offered } : o)]
        };

        var scenarios = new List<ScenarioRun>
        {
            BuildAvailabilityAndDefaultScenario(demoCatalog),
            BuildEconomySplitContinuityAndHoursScenario(demoCatalog),
            BuildSportExemptionAndSiaScenario(demoCatalog),
            BuildReligionSwitchAndBfLfPromotionScenario(demoCatalog),
            BuildItalianProfileScenario(italianCatalog),
            BuildRandomWalkScenario(demoCatalog, seed: 20261116, steps: 45),
            BuildRandomWalkScenario(demoCatalog, seed: 20261117, steps: 45),
            BuildRandomWalkScenario(italianCatalog, seed: 20261118, steps: 45)
        };

        var csResults = scenarios.Select(RunInCSharp).ToArray();
        var jsResults = RunAllInNode(jsPath, scenarios);

        Assert.Equal(csResults.Length, jsResults.Count);
        for (var s = 0; s < scenarios.Count; s++)
        {
            var name = scenarios[s].Name;
            var csSteps = csResults[s];
            var jsSteps = jsResults[s];
            Assert.Equal(csSteps.Count, jsSteps.Count);
            for (var i = 0; i < csSteps.Count; i++)
            {
                var context = $"Szenario '{name}', Schritt {i} ({scenarios[s].Actions[i].Op})";
                AssertStepEqual(context, csSteps[i], jsSteps[i]);
            }
        }
    }

    private static ScenarioRun BuildAvailabilityAndDefaultScenario(OfferCatalog catalog)
    {
        var profiles = new[]
        {
            new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No },
            new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ER", [])
                { ContinuedLanguages = ["E", "L"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No },
            new PriorEducation(Knowledge.Yes, Knowledge.No, Knowledge.Yes, Knowledge.Yes, "KR", [1, 2])
                { ContinuedLanguages = ["E", "F", "L", "I"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.Yes },
            new PriorEducation(Knowledge.No, Knowledge.Yes, Knowledge.No, Knowledge.No, null, [3, 4])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Unknown, PermanentSportExemption = Knowledge.Yes },
            new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [1, 2, 3, 4])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.Yes }
        };
        var actions = profiles.Select(p => ActionItem.SetPrerequisites(p)).ToList();
        actions.Add(ActionItem.NextStep());
        actions.Add(ActionItem.SetSelection("D-LF", [1, 2, 3, 4]));
        actions.Add(ActionItem.SetSelection("M-LF", [1, 2, 3, 4]));
        actions.Add(ActionItem.SetSelection("E-LF", [1, 2, 3, 4]));
        actions.Add(ActionItem.NextStep());
        actions.Add(ActionItem.SetOral("BIO", true));
        actions.Add(ActionItem.SetOral("G", true));
        actions.Add(ActionItem.NextStep());
        actions.Add(ActionItem.ChooseTask("PLAN-AREA-ART", "BK-BF", true));
        actions.Add(ActionItem.ChooseTask("PLAN-AREA-RELIGION", "ETH-BF", true));
        actions.Add(ActionItem.ChooseTask("PLAN-AREA-BREADTH", "CH-BF", true));
        actions.Add(ActionItem.NextStep());
        actions.Add(ActionItem.NextStep());
        return new("AvailabilityAndFullDefault", catalog, actions);
    }

    private static ScenarioRun BuildEconomySplitContinuityAndHoursScenario(OfferCatalog catalog)
    {
        var actions = new List<ActionItem>
        {
            ActionItem.SetPrerequisites(new PriorEducation(Knowledge.No, Knowledge.Yes, Knowledge.No, Knowledge.Yes, "ETH", [])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No }),
            ActionItem.NextStep(),
            ActionItem.SetSelection("W-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("E-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("BIO-LF-BILI", [1, 2, 3, 4]),
            ActionItem.NextStep(),
            // D and M are forced
            ActionItem.NextStep(),
            ActionItem.ChooseTask("PLAN-AREA-ART", "MU-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-RELIGION", "ETH-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-BREADTH", "INF-BF", true),
            ActionItem.NextStep(),
            // Attempt GK continuity gap [1, 3, 4] -> must fail identically in C# and JS
            ActionItem.SetSelection("GK-BF", [1, 3, 4]),
            ActionItem.SetSelection("GK-BF", [1, 2]),
            ActionItem.SetSelection("GK-BF", [1, 2, 3, 4]),
            ActionItem.SetSelection("GEO-BF", [3, 4]),
            // Overload hours > 37
            ActionItem.SetSelection("CH-BF", [1, 2, 3, 4]),
            ActionItem.SetSelection("PH-BF", [1, 2, 3, 4]),
            ActionItem.SetSelection("PSY-WF", [1, 2]),
            ActionItem.SetSelection("SIA-SK", [1, 2]),
            ActionItem.NextStep()
        };
        return new("EconomySplitContinuityAndHours", catalog, actions);
    }

    private static ScenarioRun BuildSportExemptionAndSiaScenario(OfferCatalog catalog)
    {
        var actions = new List<ActionItem>
        {
            ActionItem.SetPrerequisites(new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "ER", [1, 2])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.Yes }),
            ActionItem.NextStep(),
            ActionItem.SetSelection("D-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("M-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("F-LF", [1, 2, 3, 4]),
            ActionItem.NextStep(),
            ActionItem.SetOral("ER", true),
            ActionItem.SetOral("BIO", true),
            ActionItem.NextStep(),
            ActionItem.ChooseTask("PLAN-AREA-ART", "BK-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-SCIENCE", "CH-BF", true),
            ActionItem.NextStep(),
            ActionItem.SetSelection("SIA-SK", [1, 2]),
            ActionItem.SetSelection("PSY-WF", [1, 2]),
            ActionItem.NextStep()
        };
        return new("SportExemptionAndSia", catalog, actions);
    }

    private static ScenarioRun BuildReligionSwitchAndBfLfPromotionScenario(OfferCatalog catalog)
    {
        var actions = new List<ActionItem>
        {
            ActionItem.SetPrerequisites(new PriorEducation(Knowledge.No, Knowledge.No, Knowledge.No, Knowledge.No, "KR", [])
                { ContinuedLanguages = ["E", "F"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No }),
            ActionItem.NextStep(),
            ActionItem.SetSelection("D-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("M-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("W-LF", [1, 2, 3, 4]),
            ActionItem.NextStep(),
            ActionItem.SetOral("E", true),
            ActionItem.SetOral("ETH", true),
            ActionItem.NextStep(),
            ActionItem.ChooseTask("PLAN-AREA-ART", "BK-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-SCIENCE", "BIO-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-BREADTH", "CH-BF", true),
            ActionItem.NextStep(),
            // Try invalid ER-BF (blocked because grade 10 is KR)
            ActionItem.SetSelection("ER-BF", [1, 2, 3, 4]),
            // Go back to step 1, replace W-LF with E-LF (removes E-BF and E oral, stores displacedBasic)
            ActionItem.GoToStep(1),
            ActionItem.SetSelection("W-LF", []),
            ActionItem.SetSelection("E-LF", [1, 2, 3, 4]),
            // Revert E-LF -> restores E-BF and E oral
            ActionItem.SetSelection("E-LF", []),
            ActionItem.SetSelection("W-LF", [1, 2, 3, 4]),
            ActionItem.NextStep(),
            ActionItem.NextStep(),
            ActionItem.NextStep(),
            ActionItem.NextStep()
        };
        return new("ReligionSwitchAndBfLfPromotion", catalog, actions);
    }

    private static ScenarioRun BuildItalianProfileScenario(OfferCatalog catalog)
    {
        var actions = new List<ActionItem>
        {
            ActionItem.SetPrerequisites(new PriorEducation(Knowledge.Yes, Knowledge.No, Knowledge.No, Knowledge.No, "ETH", [])
                { ContinuedLanguages = ["E", "I"], ReligionEthicsPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No }),
            ActionItem.NextStep(),
            ActionItem.SetSelection("D-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("M-LF", [1, 2, 3, 4]),
            ActionItem.SetSelection("BIO-LF", [1, 2, 3, 4]),
            ActionItem.NextStep(),
            ActionItem.SetOral("I", true),
            ActionItem.SetOral("G", true),
            ActionItem.NextStep(),
            ActionItem.ChooseTask("PLAN-AREA-ART", "BK-BF", true),
            ActionItem.ChooseTask("PLAN-AREA-RELIGION", "ETH-BF", true),
            ActionItem.NextStep(),
            ActionItem.NextStep()
        };
        return new("ItalianProfile", catalog, actions);
    }

    private static ScenarioRun BuildRandomWalkScenario(OfferCatalog catalog, int seed, int steps)
    {
        var rng = new Random(seed);
        var actions = new List<ActionItem>();
        var offers = catalog.Offers.ToArray();
        var subjects = catalog.Subjects.Select(s => s.Id).ToArray();

        for (var i = 0; i < steps; i++)
        {
            var kind = rng.Next(100);
            if (kind < 15)
            {
                var hasF = rng.Next(2) == 1;
                var hasL = !hasF || rng.Next(2) == 1;
                var hasI = rng.Next(2) == 1;
                var langs = new List<string> { "E" };
                if (hasF) langs.Add("F");
                if (hasL) langs.Add("L");
                if (hasI) langs.Add("I");
                string? rel = rng.Next(4) switch { 0 => "ER", 1 => "KR", 2 => "ETH", _ => null };
                ImmutableArray<int> sport = rng.Next(4) switch { 1 => [1, 2], 2 => [3, 4], 3 => [1, 2, 3, 4], _ => [] };
                actions.Add(ActionItem.SetPrerequisites(new PriorEducation(
                    hasI ? Knowledge.Yes : Knowledge.No,
                    rng.Next(2) == 1 ? Knowledge.Yes : Knowledge.No,
                    rng.Next(2) == 1 ? Knowledge.Yes : Knowledge.No,
                    rng.Next(2) == 1 ? Knowledge.Yes : Knowledge.No,
                    rel,
                    sport)
                {
                    ContinuedLanguages = [.. langs],
                    ReligionEthicsPermission = rel is null ? Knowledge.Unknown : Knowledge.Yes,
                    PermanentSportExemption = sport.IsEmpty ? Knowledge.No : Knowledge.Yes
                }));
            }
            else if (kind < 30)
            {
                actions.Add(ActionItem.NextStep());
            }
            else if (kind < 38)
            {
                actions.Add(ActionItem.PreviousStep());
            }
            else if (kind < 44)
            {
                actions.Add(ActionItem.GoToStep(rng.Next(0, 5)));
            }
            else if (kind < 62)
            {
                var offer = offers[rng.Next(offers.Length)];
                var semMode = rng.Next(4);
                int[] sems = semMode switch
                {
                    0 => [],
                    1 => [1, 2],
                    2 => [3, 4],
                    _ => [.. offer.Schedule.Select(h => h.Semester)]
                };
                actions.Add(ActionItem.SetSelection(offer.Id, sems));
            }
            else if (kind < 80)
            {
                actions.Add(ActionItem.SetOral(subjects[rng.Next(subjects.Length)], rng.Next(2) == 1));
            }
            else
            {
                var taskIds = new[] { "PLAN-AREA-ART", "PLAN-AREA-RELIGION", "PLAN-AREA-LANGUAGE", "PLAN-AREA-SCIENCE", "PLAN-AREA-BREADTH" };
                var offer = offers[rng.Next(offers.Length)];
                actions.Add(ActionItem.ChooseTask(taskIds[rng.Next(taskIds.Length)], offer.Id, rng.Next(2) == 1));
            }
        }
        return new($"RandomWalk_{seed}", catalog, actions);
    }

    private static List<StepSnapshot> RunInCSharp(ScenarioRun scenario)
    {
        var session = new StudentSession(scenario.Catalog, "demo-testversion", new NullChoiceStore(), allowSelfServicePrerequisites: true);
        var results = new List<StepSnapshot>(scenario.Actions.Count);
        foreach (var action in scenario.Actions)
        {
            string? thrown = null;
            try
            {
                switch (action.Op)
                {
                    case "setPrerequisites":
                        session.SetTestPrerequisites(action.Prerequisites!);
                        break;
                    case "nextStep":
                        session.NextStep();
                        break;
                    case "previousStep":
                        session.PreviousStep();
                        break;
                    case "goToStep":
                        session.GoToStep(action.StepTarget!.Value);
                        break;
                    case "setSelection":
                        session.SetSelection(action.OfferId!, action.Semesters!);
                        break;
                    case "setOral":
                        session.SetOral(action.SubjectId!, action.Selected!.Value);
                        break;
                    case "chooseTaskOption":
                        session.ChooseTaskOption(action.TaskId!, action.OfferId!, action.Selected!.Value);
                        break;
                    default:
                        throw new InvalidOperationException("Unbekannte Testaktion: " + action.Op);
                }
            }
            catch (InvalidOperationException ex)
            {
                thrown = ex.Message;
            }
            results.Add(CaptureCSharp(session, thrown));
        }
        return results;
    }

    private static StepSnapshot CaptureCSharp(StudentSession session, string? error)
    {
        var validation = session.Validation;
        return new StepSnapshot(
            Error: error,
            Step: session.Step,
            FirstStep: session.FirstStep,
            StepError: session.StepError,
            CountedCourses: session.CountedCourses,
            LfChangeMessage: session.LfChangeMessage,
            AutomaticMessage: session.AutomaticMessage,
            Availabilities: [.. session.Catalog.Offers.Select(o =>
            {
                var a = session.Availability(o);
                return new AvailabilitySnap(o.Id, a.CanSelect, EnumString(a.Status), a.Explanation);
            })],
            ValidationStatus: EnumString(validation.Status),
            ValidationMessages: [.. validation.Messages.Select(m => new MessageSnap(
                m.Rule.Id,
                EnumString(m.Rule.Severity),
                [.. m.SubjectIds],
                [.. m.Semesters],
                NormalizePlanHours(m.Rule.Id, m.Explanation)))],
            Tasks: [.. session.Tasks.Select(t => new TaskSnap(
                t.Id, t.Rule.Id, t.Choose, t.Open,
                [.. t.OptionOfferIds], [.. t.ChosenOfferIds], [.. t.FulfilledBySubjects],
                [.. t.Restricted.Select(r => r.OfferId)]))],
            Plan: [.. session.Plan.Select(p => new PlanRowSnap(
                p.Label, p.OfferId,
                p.Type is { } ct ? EnumString(ct) : null,
                EnumString(p.Variant),
                [.. p.Semesters], p.IsAutomatic, p.IsOral,
                p.Issue is { } iss ? EnumString(iss) : null,
                p.IsOpen, p.Step,
                p.IssueText is null ? null : NormalizeAllPlanHours(p.IssueText)))],
            Summary: [.. session.Summary.Select(s => new SummarySnap(s.Semester, s.CourseCount, s.WeeklyHours))],
            ExamOptions: [.. session.ExamOptions.Select(e => new ExamSnap(e.First, e.Second, e.MinimumCredits))],
            OralCandidates: [.. session.OralCandidates],
            ForcedOral: [.. session.ForcedOral],
            UsefulOralCandidates: [.. session.UsefulOralCandidates],
            PlanningNotes: [.. session.PlanningNotes]
        );
    }

    private static List<List<StepSnapshot>> RunAllInNode(string jsPath, List<ScenarioRun> scenarios)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gsm-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, "input.json");
        var runnerPath = Path.Combine(tempDir, "runner.cjs");
        try
        {
            var payload = scenarios.Select(s => new
            {
                name = s.Name,
                catalog = s.Catalog,
                actions = s.Actions
            }).ToArray();
            File.WriteAllText(inputPath, JsonSerializer.Serialize(payload, DocumentJson.Options), new UTF8Encoding(false));

            var runnerCode = $$"""
const fs = require("fs");
const api = require({{JsonSerializer.Serialize(jsPath)}});
const scenarios = JSON.parse(fs.readFileSync({{JsonSerializer.Serialize(inputPath)}}, "utf8"));

function normalizePlanHours(ruleId, text) {
  if (ruleId !== "PLAN-HOURS") return text;
  return text.replace(/Durchschnitt (\d+),(\d+) Wochenstunden/g, "Durchschnitt $1.$2 Wochenstunden");
}
function normalizeAllPlanHours(text) {
  return text.replace(/Durchschnitt (\d+),(\d+) Wochenstunden/g, "Durchschnitt $1.$2 Wochenstunden");
}

function capture(session, error) {
  const val = session.validation;
  return {
    error: error,
    step: session.step,
    firstStep: session.firstStep,
    stepError: session.stepError,
    countedCourses: session.countedCourses,
    lfChangeMessage: session.lfChangeMessage,
    automaticMessage: session.automaticMessage,
    availabilities: session.catalog.offers.map((o) => {
      const a = session.availability(o);
      return { offerId: o.id, canSelect: a.canSelect, status: a.status, explanation: a.explanation };
    }),
    validationStatus: val.status,
    validationMessages: val.messages.map((m) => ({
      ruleId: m.rule.id,
      severity: m.rule.severity,
      subjectIds: m.subjectIds,
      semesters: m.semesters,
      explanation: normalizePlanHours(m.rule.id, m.explanation)
    })),
    tasks: session.tasks.map((t) => ({
      id: t.id,
      ruleId: t.rule.id,
      choose: t.choose,
      open: t.open,
      optionOfferIds: t.optionOfferIds,
      chosenOfferIds: t.chosenOfferIds,
      fulfilledBySubjects: t.fulfilledBySubjects,
      restrictedOfferIds: t.restricted.map((r) => r.offerId)
    })),
    plan: session.plan.map((p) => ({
      label: p.label,
      offerId: p.offerId,
      type: p.type,
      variant: p.variant,
      semesters: p.semesters,
      isAutomatic: p.isAutomatic,
      isOral: p.isOral,
      issue: p.issue,
      isOpen: p.isOpen,
      step: p.step,
      issueText: p.issueText === null ? null : normalizeAllPlanHours(p.issueText)
    })),
    summary: session.summary.map((s) => ({
      semester: s.semester,
      courseCount: s.courseCount,
      weeklyHours: s.weeklyHours
    })),
    examOptions: session.examOptions.map((e) => ({
      first: e.first,
      second: e.second,
      minimumCredits: e.minimumCredits
    })),
    oralCandidates: session.oralCandidates,
    forcedOral: session.forcedOral,
    usefulOralCandidates: session.usefulOralCandidates,
    planningNotes: session.planningNotes
  };
}

const out = scenarios.map((sc) => {
  const session = api.createSession(sc.catalog, { allowSelfServicePrerequisites: true });
  return sc.actions.map((a) => {
    let err = null;
    try {
      if (a.op === "setPrerequisites") session.setTestPrerequisites(a.prerequisites);
      else if (a.op === "nextStep") session.nextStep();
      else if (a.op === "previousStep") session.previousStep();
      else if (a.op === "goToStep") session.goToStep(a.stepTarget);
      else if (a.op === "setSelection") session.setSelection(a.offerId, a.semesters);
      else if (a.op === "setOral") session.setOral(a.subjectId, a.selected);
      else if (a.op === "chooseTaskOption") session.chooseTaskOption(a.taskId, a.offerId, a.selected);
      else throw new Error("Unknown op: " + a.op);
    } catch (e) {
      err = e && e.message ? e.message : String(e);
    }
    return capture(session, err);
  });
});
process.stdout.write(JSON.stringify(out));
""";
            File.WriteAllText(runnerPath, runnerCode, new UTF8Encoding(false));

            var psi = new ProcessStartInfo("node", $"\"{runnerPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("node konnte nicht gestartet werden.");
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            Assert.True(proc.ExitCode == 0, $"Node.js-Fehler (Exit {proc.ExitCode}): {stderr}");

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return JsonSerializer.Deserialize<List<List<StepSnapshot>>>(stdout, options)
                ?? throw new InvalidOperationException("Leere Node-Ausgabe.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static void AssertStepEqual(string ctx, StepSnapshot cs, StepSnapshot js)
    {
        Assert.True(cs.Error == js.Error, $"{ctx}: Error C#='{cs.Error}' vs JS='{js.Error}'");
        Assert.True(cs.Step == js.Step, $"{ctx}: Step C#={cs.Step} vs JS={js.Step}");
        Assert.Equal(cs.FirstStep, js.FirstStep);
        Assert.True(cs.StepError == js.StepError, $"{ctx}: StepError C#='{cs.StepError}' vs JS='{js.StepError}'");
        Assert.True(cs.CountedCourses == js.CountedCourses, $"{ctx}: CountedCourses C#={cs.CountedCourses} vs JS={js.CountedCourses}");
        Assert.True(cs.LfChangeMessage == js.LfChangeMessage, $"{ctx}: LfChangeMessage C#='{cs.LfChangeMessage}' vs JS='{js.LfChangeMessage}'");
        Assert.True(cs.AutomaticMessage == js.AutomaticMessage, $"{ctx}: AutomaticMessage C#='{cs.AutomaticMessage}' vs JS='{js.AutomaticMessage}'");
        Assert.Equal(cs.Availabilities, js.Availabilities);
        Assert.Equal(cs.ValidationStatus, js.ValidationStatus);
        Assert.Equal(cs.ValidationMessages, js.ValidationMessages);
        Assert.Equal(cs.Tasks, js.Tasks);
        Assert.Equal(cs.Plan, js.Plan);
        Assert.Equal(cs.Summary, js.Summary);
        Assert.Equal(cs.ExamOptions, js.ExamOptions);
        Assert.Equal(cs.OralCandidates, js.OralCandidates);
        Assert.Equal(cs.ForcedOral, js.ForcedOral);
        Assert.Equal(cs.UsefulOralCandidates, js.UsefulOralCandidates);
        Assert.Equal(cs.PlanningNotes, js.PlanningNotes);
    }

    private static string EnumString<T>(T value) where T : struct, Enum
    {
        var raw = JsonSerializer.Serialize(value, DocumentJson.Options);
        return raw.Trim('"');
    }

    private static string NormalizePlanHours(string ruleId, string text) =>
        ruleId == "PLAN-HOURS" ? NormalizeAllPlanHours(text) : text;

    private static string NormalizeAllPlanHours(string text) =>
        Regex.Replace(text, @"Durchschnitt (\d+),(\d+) Wochenstunden", "Durchschnitt $1.$2 Wochenstunden");

    private sealed class NullChoiceStore : IChoiceStore
    {
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<StoredChoice?>(null);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredChoice(choice, "mem"));
    }

    private sealed record ScenarioRun(string Name, OfferCatalog Catalog, List<ActionItem> Actions);

    private sealed record ActionItem(
        string Op,
        PriorEducation? Prerequisites = null,
        int? StepTarget = null,
        string? OfferId = null,
        int[]? Semesters = null,
        string? SubjectId = null,
        bool? Selected = null,
        string? TaskId = null)
    {
        public static ActionItem SetPrerequisites(PriorEducation p) => new("setPrerequisites", Prerequisites: p);
        public static ActionItem NextStep() => new("nextStep");
        public static ActionItem PreviousStep() => new("previousStep");
        public static ActionItem GoToStep(int step) => new("goToStep", StepTarget: step);
        public static ActionItem SetSelection(string offerId, int[] semesters) => new("setSelection", OfferId: offerId, Semesters: semesters);
        public static ActionItem SetOral(string subjectId, bool selected) => new("setOral", SubjectId: subjectId, Selected: selected);
        public static ActionItem ChooseTask(string taskId, string offerId, bool selected) => new("chooseTaskOption", OfferId: offerId, Selected: selected, TaskId: taskId);
    }

    private sealed record AvailabilitySnap(string OfferId, bool CanSelect, string Status, string Explanation);
    private sealed record MessageSnap(string RuleId, string Severity, string[] SubjectIds, int[] Semesters, string Explanation)
    {
        public bool Equals(MessageSnap? other) => other is not null
            && RuleId == other.RuleId && Severity == other.Severity && Explanation == other.Explanation
            && SubjectIds.SequenceEqual(other.SubjectIds) && Semesters.SequenceEqual(other.Semesters);
        public override int GetHashCode() => HashCode.Combine(RuleId, Severity, Explanation);
    }
    private sealed record TaskSnap(string Id, string RuleId, int Choose, int Open, string[] OptionOfferIds, string[] ChosenOfferIds, string[] FulfilledBySubjects, string[] RestrictedOfferIds)
    {
        public bool Equals(TaskSnap? other) => other is not null
            && Id == other.Id && RuleId == other.RuleId && Choose == other.Choose && Open == other.Open
            && OptionOfferIds.SequenceEqual(other.OptionOfferIds) && ChosenOfferIds.SequenceEqual(other.ChosenOfferIds)
            && FulfilledBySubjects.SequenceEqual(other.FulfilledBySubjects) && RestrictedOfferIds.SequenceEqual(other.RestrictedOfferIds);
        public override int GetHashCode() => HashCode.Combine(Id, RuleId, Choose, Open);
    }
    private sealed record PlanRowSnap(string Label, string? OfferId, string? Type, string Variant, int[] Semesters, bool IsAutomatic, bool IsOral, string? Issue, bool IsOpen, int Step, string? IssueText)
    {
        public bool Equals(PlanRowSnap? other) => other is not null
            && Label == other.Label && OfferId == other.OfferId && Type == other.Type && Variant == other.Variant
            && Semesters.SequenceEqual(other.Semesters) && IsAutomatic == other.IsAutomatic && IsOral == other.IsOral
            && Issue == other.Issue && IsOpen == other.IsOpen && Step == other.Step && IssueText == other.IssueText;
        public override int GetHashCode() => HashCode.Combine(Label, OfferId, Type, Variant, IsAutomatic, IsOral, Issue, Step);
    }
    private sealed record SummarySnap(int Semester, int CourseCount, int WeeklyHours);
    private sealed record ExamSnap(string First, string Second, int MinimumCredits);
    private sealed record StepSnapshot(
        string? Error,
        int Step,
        int FirstStep,
        string? StepError,
        int CountedCourses,
        string LfChangeMessage,
        string AutomaticMessage,
        AvailabilitySnap[] Availabilities,
        string ValidationStatus,
        MessageSnap[] ValidationMessages,
        TaskSnap[] Tasks,
        PlanRowSnap[] Plan,
        SummarySnap[] Summary,
        ExamSnap[] ExamOptions,
        string[] OralCandidates,
        string[] ForcedOral,
        string[] UsefulOralCandidates,
        string[] PlanningNotes);
}
