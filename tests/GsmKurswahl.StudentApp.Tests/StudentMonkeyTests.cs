using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GsmKurswahl.Application;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;
using GsmKurswahl.Storage;
using GsmKurswahl.Tests;

namespace GsmKurswahl.StudentApp.Tests;

// Randomized click sequences through the real student window (forward/back, many changes, save/load).
// After every action the session and the visible controls are checked against invariants.
public class StudentMonkeyTests
{
    private static readonly string[] Technical = ["Sequence contains", "Object reference", "Index was out", "Nullable object", "Specified argument"];

    public static IEnumerable<object[]> Profiles() =>
    [
        ["missing"], ["verified"], ["sport-exempt"], ["religion-kr"]
    ];

    private static ProfileContext Profile(string name) => name switch
    {
        "verified" => new(ProfileStatus.Verified, new PriorEducation(Knowledge.No, Knowledge.Yes, Knowledge.Unknown, Knowledge.Yes, "ETH", [])
        { ContinuedLanguages = ["F"], ReligionEthicsPermission = Knowledge.Yes, ReligionEthicsExamPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No }),
        "sport-exempt" => new(ProfileStatus.Verified, PriorEducation.Unknown with { SportExemptSemesters = [2], PermanentSportExemption = Knowledge.No }),
        "religion-kr" => new(ProfileStatus.Verified, PriorEducation.Unknown with { ReligionEthicsGrade10 = "KR", SportExemptSemesters = [] }),
        _ => ProfileContext.Missing
    };

    [AvaloniaTheory]
    [MemberData(nameof(Profiles))]
    public async Task RandomClickSequencesKeepSessionAndControlsConsistent(string profileName)
    {
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        var runs = int.TryParse(Environment.GetEnvironmentVariable("GSM_MONKEY_RUNS"), out var r) ? r : 4;
        var steps = int.TryParse(Environment.GetEnvironmentVariable("GSM_MONKEY_STEPS"), out var s) ? s : 120;
        var findings = new Dictionary<string, string>();
        for (var seed = 0; seed < runs; seed++)
            await Run(catalog, Profile(profileName), seed, steps, findings);
        var report = Environment.GetEnvironmentVariable("GSM_MONKEY_REPORT");
        if (report is { Length: > 0 })
            File.AppendAllText(report, $"=== Profil {profileName}: {findings.Count} Befundarten ===\n" + string.Join("\n", findings.Select(f => $"[{f.Key}]\n{f.Value}\n")) + "\n");
        if (Environment.GetEnvironmentVariable("GSM_MONKEY_EXPLORE") is not { Length: > 0 })
            Assert.True(findings.Count == 0, string.Join("\n\n", findings.Select(f => $"[{f.Key}]\n{f.Value}")));
    }

    private static async Task Run(OfferCatalog catalog, ProfileContext profile, int seed, int steps, Dictionary<string, string> findings)
    {
        var random = new Random(seed);
        var store = new MemoryStore();
        var session = new StudentSession(catalog, "demo-001", store, profile);
        var window = new MainWindow(session, "demo.json", "demo-account");
        var errorBlock = (TextBlock)typeof(MainWindow).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        var trace = new List<string>();
        void Report(string kind, string detail)
        {
            if (findings.ContainsKey(kind)) return;
            findings[kind] = $"Seed {seed}, Schritt {session.Step}: {detail}\nLetzte Aktionen: {string.Join(" → ", trace.TakeLast(14))}";
        }
        try
        {
            window.Show();
            for (var i = 0; i < steps; i++)
            {
                window.UpdateLayout();
                var controls = window.GetVisualDescendants().OfType<Control>().Where(c => c.Name is { Length: > 0 } && c.IsEffectivelyVisible && c.IsEffectivelyEnabled).ToList();
                var toggles = controls.OfType<ToggleButton>().Where(t => t.Name!.Split('_')[0] is "Offer" or "Option" or "Oral" or "Sem"
                    && !(t is RadioButton && t.IsChecked == true)).ToList();
                var buttons = controls.OfType<Button>().Where(b => b is not ToggleButton && (b.Name is "NextStep" or "PreviousStep" or "SaveButton" or "TaskDone"
                    || b.Name!.StartsWith("Step_") || b.Name.StartsWith("Plan_") || b.Name.StartsWith("Task_")
                    || (b.Name == "LoadButton" && !session.IsDirty))).ToList();
                // File actions are rare; at blocked steps 1/2 the monkey mostly works towards a valid state.
                if (random.NextDouble() < 0.85) buttons.RemoveAll(b => b.Name is "SaveButton" or "LoadButton");
                if (session.StepError is not null && random.NextDouble() < 0.8)
                {
                    var lf = session.Choice.Selections.Count(x => x.Type == CourseType.Lf);
                    var oral = session.Choice.PlannedOralSubjects.Length;
                    var focus = session.Step == 1
                        ? toggles.Where(t => t.Name!.EndsWith("_LF") && (lf < 3 ? t.IsChecked != true : lf > 3 ? t.IsChecked == true : true)).ToList()
                        : toggles.Where(t => t.Name!.StartsWith("Oral_") && (oral < 2 ? t.IsChecked != true : true)).ToList();
                    if (focus.Count > 0) { toggles = focus; buttons.Clear(); }
                }
                // Prefer toggles so selections change a lot; navigation still happens often.
                var useToggle = toggles.Count > 0 && (buttons.Count == 0 || random.NextDouble() < 0.6);
                var before = session.Choice;
                var stepBefore = session.Step;
                string? toggledName = null; bool intended = false;
                try
                {
                    if (useToggle)
                    {
                        var toggle = toggles[random.Next(toggles.Count)];
                        toggledName = toggle.Name; intended = toggle.IsChecked != true;
                        trace.Add($"{toggle.Name}={(intended ? "an" : "aus")}");
                        toggle.IsChecked = intended;
                    }
                    else
                    {
                        // Next is weighted so that later steps are reached regularly.
                        var button = buttons.FirstOrDefault(b => b.Name == "NextStep" && random.NextDouble() < 0.35) ?? buttons[random.Next(buttons.Count)];
                        trace.Add(button.Name!);
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        for (var wait = 0; wait < 100 && session.IsBusy; wait++) await Task.Delay(5);
                    }
                }
                catch (Exception ex) { Report("Ausnahme " + ex.GetType().Name, ex.ToString()); break; }
                window.UpdateLayout();
                Check(window, session, errorBlock.Text ?? "", toggledName, intended, before, stepBefore, Report);
            }
        }
        finally { if (session.IsDirty && !session.LoadFailed) await session.SaveAsync(); window.Close(); }
    }

    private static void Check(MainWindow window, StudentSession session, string error, string? toggledName, bool intended,
        CourseChoice before, int stepBefore, Action<string, string> report)
    {
        var choice = session.Choice;
        try { ModelGuard.Choice(choice); } catch (ArgumentException ex) { report("ModelGuard", ex.Message); }
        if (Technical.Any(error.Contains)) report("Technische Fehlermeldung", error);
        ValidationResult validation;
        try { validation = session.Validation; } catch (Exception ex) { report("Validierung wirft", ex.ToString()); return; }
        foreach (var id in new[] { "SUBJECT-DUPLICATE", "GSM-RELIGION-EXCLUSIVE", "OFFER-SELECTION" })
            if (validation.Messages.Any(m => m.Rule.Id == id)) report("Regelverstoß über UI: " + id, Describe(session));
        // With a confirmed sport exemption single half-years are offered on purpose; the validator limits their number.
        if (session.Step >= 3 && validation.Messages.Any(m => m.Rule.Id == "AGVO-SEMESTERS")
            && session.Profile.Prerequisites.SportExemptSemesters is not { Length: > 0 })
            report("Einzelhalbjahr ab Schritt 3: AGVO-SEMESTERS", Describe(session));
        var lfCount = choice.Selections.Count(x => x.Type == CourseType.Lf);
        if (lfCount > 3) report("Mehr als drei LF wählbar", Describe(session));
        if (session.Step >= 2 && ChoiceWorkflow.LfError(choice, session.Catalog, session.Profile) is { } lfError)
            report("Schritt >= 2 mit ungültigen LF", lfError + " " + Describe(session));
        if (session.Step >= 3 && ChoiceWorkflow.OralError(choice, session.Catalog, session.Profile) is { } oralError)
            report("Schritt >= 3 mit ungültiger mündlicher Planung", oralError + " " + Describe(session));
        if (session.Step >= 3)
        {
            foreach (var required in ChoiceWorkflow.Required(choice, session.Profile))
                if (!choice.Selections.Any(x => x.SubjectId == required.SubjectId && required.Semesters.All(x.Semesters.Contains))
                    && !session.AutomaticMessage.Contains($"({required.SubjectId})"))
                    report("Pflichtbelegung fehlt ab Schritt 3: " + required.SubjectId, $"benötigt HJ {string.Join(",", required.Semesters)}. " + Describe(session));
        }
        if (session.LfChangeMessage.Length > 0)
        {
            var subject = session.Catalog.Subjects.FirstOrDefault(x => session.LfChangeMessage.StartsWith(x.Name + " ist jetzt Leistungsfach"));
            if (subject is not null && !choice.Selections.Any(x => x.Type == CourseType.Lf && x.SubjectId == subject.Id))
                report("Veralteter LF-Wechselhinweis", session.LfChangeMessage + " " + Describe(session));
        }
        var hours = session.Summary.Any(x => x.WeeklyHours > 37);
        var warning = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "HoursWarning");
        if (warning.IsVisible != hours) report("Stundenwarnung inkonsistent", Describe(session));

        // Visible toggles must reflect the model the student will save.
        foreach (var toggle in window.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Name is { Length: > 0 } && t.IsEffectivelyVisible))
        {
            var parts = toggle.Name!.Split('_', 2);
            if (parts.Length < 2) continue;
            bool? expected = parts[0] switch
            {
                "Offer" => choice.Selections.Any(x => x.OfferId.Replace('-', '_') == parts[1]),
                "Option" => choice.Selections.Any(x => x.OfferId.Replace('-', '_') == parts[1] && AcademicRules.Four(x)),
                "Oral" => choice.PlannedOralSubjects.Contains(parts[1]),
                _ => null
            };
            if (expected is { } value && (toggle.IsChecked == true) != value)
                report($"Anzeige weicht vom Modell ab ({parts[0]})", $"{toggle.Name} zeigt {(toggle.IsChecked == true ? "gewählt" : "nicht gewählt")}, Modell: {(value ? "gewählt" : "nicht gewählt")}. " + Describe(session));
        }
        if (toggledName is not null && error.Length == 0)
        {
            var again = window.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(t => t.Name == toggledName);
            if (again is not null && (again.IsChecked == true) != intended && before == choice)
                report("Klick ohne Wirkung und ohne Meldung", $"{toggledName} -> {(intended ? "an" : "aus")}. " + Describe(session));
            if (again is not null && (again.IsChecked == true) != intended && before != choice)
                report("Klick ändert Wahl, Kästchen springt zurück", $"{toggledName} -> {(intended ? "an" : "aus")}. Vorher: {Selections(before)} Nachher: {Selections(choice)}");
        }
        if (toggledName is not null && error.Length > 0 && before != choice)
            report("Fehlermeldung, aber Wahl trotzdem verändert", error + " Vorher: " + Selections(before) + " Nachher: " + Selections(choice));
        // Removal of other courses as a side effect of a single click (other than documented LF promotion).
        if (toggledName is not null && before != choice)
        {
            var lost = before.Selections.Where(b => !choice.Selections.Any(a => a.SubjectId == b.SubjectId)).Select(b => b.OfferId)
                .Where(id => id.Replace('-', '_') != toggledName.Split('_', 2)[1]).ToArray();
            var promoted = toggledName.EndsWith("_LF") ? toggledName.Split('_')[1] : null;
            lost = [.. lost.Where(id => !(promoted is not null && id.StartsWith(promoted + "-")))];
            if (lost.Length > 0 && toggledName.Split('_')[0] is "Offer" or "Oral")
                report("Klick entfernt andere Kurse", $"{toggledName}: entfernt {string.Join(",", lost)}");
            // Clicking a course that is already selected (but shown unchecked) must not delete a different course.
            var clicked = toggledName.Split('_', 2)[1];
            if (lost.Length > 0 && toggledName.StartsWith("Option_") && before.Selections.Any(x => x.OfferId.Replace('-', '_') == clicked && AcademicRules.Four(x)))
                report("Klick auf bereits belegtes Fach löscht anderen Kurs", $"{toggledName}: entfernt {string.Join(",", lost)}. Vorher: {Selections(before)} Nachher: {Selections(choice)}");
        }
        // Each task answer must be one of the courses currently shown as chosen; a chosen course of a task must not jump to another task.
        if (session.Step == 3)
        {
            var chosen = session.Tasks.SelectMany(t => t.ChosenOfferIds).ToArray();
            if (chosen.Distinct().Count() != chosen.Length) report("Kurs erfüllt zwei Fragen gleichzeitig", string.Join(",", chosen));
        }
        var plan = session.Plan;
        if (plan.Where(p => p.OfferId is not null).GroupBy(p => p.OfferId).Any(g => g.Count() > 1)) report("Doppelte Planzeile", Describe(session));
        if (stepBefore != session.Step && Math.Abs(stepBefore - session.Step) > 1 && session.Step > stepBefore) report("Vorwärtssprung über mehrere Schritte", $"{stepBefore} -> {session.Step}");
    }

    // Completed choice -> back to LF or oral planning -> change -> forward again, repeated several times.
    [AvaloniaTheory]
    [MemberData(nameof(Profiles))]
    public async Task CompletedChoiceRevisedViaStepsOneAndTwoStaysConsistent(string profileName)
    {
        var catalog = JsonDocuments.ReadCatalog(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "fachangebot-demo.json")));
        var runs = int.TryParse(Environment.GetEnvironmentVariable("GSM_MONKEY_RUNS"), out var r) ? r : 6;
        var findings = new Dictionary<string, string>();
        for (var seed = 0; seed < runs; seed++)
        {
            var random = new Random(1000 + seed);
            var session = new StudentSession(catalog, "demo-001", new MemoryStore(), Profile(profileName));
            var window = new MainWindow(session, "demo.json", "demo-account");
            var errorBlock = (TextBlock)typeof(MainWindow).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var trace = new List<string>();
            void Report(string kind, string detail)
            {
                if (!findings.ContainsKey(kind))
                    findings[kind] = $"Seed {1000 + seed}, Schritt {session.Step}: {detail}\nLetzte Aktionen: {string.Join(" → ", trace.TakeLast(24))}";
            }
            List<T> Visible<T>(string prefix) where T : Control
            {
                window.UpdateLayout();
                return [.. window.GetVisualDescendants().OfType<T>().Where(c => c.Name?.StartsWith(prefix) == true && c.IsEffectivelyVisible && c.IsEffectivelyEnabled)];
            }
            void Act(string label, Action action)
            {
                trace.Add(label);
                var before = session.Choice; var step = session.Step;
                try { action(); } catch (Exception ex) { Report("Ausnahme " + ex.GetType().Name, ex.ToString()); throw; }
                window.UpdateLayout();
                Check(window, session, errorBlock.Text ?? "", null, false, before, step, Report);
            }
            void Toggle(ToggleButton t, bool value) => Act($"{t.Name}={(value ? "an" : "aus")}", () => t.IsChecked = value);
            void Click(string name) => Act(name, () => Visible<Button>(name).Single(b => b.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bool Complete()
            {
                for (var guard = 0; guard < 80 && session.Step < StudentSession.LastStep; guard++)
                {
                    if (session.Step == 1)
                    {
                        var lf = Visible<ToggleButton>("Offer_").Where(t => t.Name!.EndsWith("_LF")).ToList();
                        var count = session.Choice.Selections.Count(s => s.Type == CourseType.Lf);
                        if (count < 3) Toggle(lf.Where(t => t.IsChecked != true).OrderBy(_ => random.Next()).First(), true);
                        else if (session.StepError is not null) Toggle(lf.Where(t => t.IsChecked == true).OrderBy(_ => random.Next()).First(), false);
                        else Click("NextStep");
                    }
                    else if (session.Step == 2)
                    {
                        var oral = Visible<ToggleButton>("Oral_");
                        if (session.StepError is null) Click("NextStep");
                        else if (session.Choice.PlannedOralSubjects.Length < 2 && oral.Any(t => t.IsChecked != true))
                            Toggle(oral.Where(t => t.IsChecked != true).OrderBy(_ => random.Next()).First(), true);
                        else if (oral.Any(t => t.IsChecked == true)) Toggle(oral.Where(t => t.IsChecked == true).OrderBy(_ => random.Next()).First(), false);
                        else { Click("PreviousStep"); }
                    }
                    else if (session.Step == 3)
                    {
                        var open = Visible<ToggleButton>("Option_").Where(t => t.IsChecked != true).ToList();
                        if (session.Tasks.Any(t => !t.IsSatisfied) && open.Count > 0) Toggle(open[random.Next(open.Count)], true);
                        else Click("NextStep");
                    }
                    else
                    {
                        var extras = Visible<ToggleButton>("Offer_").Where(t => t.IsChecked != true).ToList();
                        if (session.CountedCourses < ChoiceWorkflow.MinimumCourses && extras.Count > 0 && random.NextDouble() < 0.7) Toggle(extras[random.Next(extras.Count)], true);
                        else Click("NextStep");
                    }
                }
                return session.Step == StudentSession.LastStep;
            }
            void AtEnd()
            {
                var oral = session.Choice.PlannedOralSubjects.Order().ToArray();
                if (oral.Length == 2 && !session.ExamOptions.Any(e => new[] { e.First, e.Second }.Order().SequenceEqual(oral))
                    && AcademicRules.MinimumCredits(session.Choice, oral, session.Profile) <= 40)
                    Report("Geplante mündliche Fächer fehlen in den möglichen Kombinationen", string.Join("/", oral) + " " + Describe(session));
                foreach (var subject in session.Choice.PlannedOralSubjects)
                    if (!session.Choice.Selections.Any(s => s.SubjectId == subject && ChoiceWorkflow.OralSemesters(session.Choice, subject).All(s.Semesters.Contains))
                        && !(subject == "SP" && session.Profile.Prerequisites.SportExemptSemesters is { Length: > 0 }))
                        Report("Mündliches Fach am Ende nicht vollständig belegt", subject + " " + Describe(session));
            }
            try
            {
                window.Show();
                if (!Complete()) { Report("Erstwahl nicht abschließbar", Describe(session)); continue; }
                AtEnd();
                for (var revision = 0; revision < 6; revision++)
                {
                    var completed = session.Choice;
                    var target = random.Next(1, 3);
                    trace.Add($"--- Revision {revision}: zurück zu Schritt {target} ---");
                    Click("Step_" + target);
                    if (session.Step != target) { Report("Rücksprung über Schrittleiste fehlgeschlagen", $"Ziel {target}"); break; }
                    for (var change = random.Next(1, 4); change > 0; change--)
                    {
                        var options = target == 1 ? Visible<ToggleButton>("Offer_") : Visible<ToggleButton>("Oral_");
                        if (options.Count == 0) break;
                        var t = options[random.Next(options.Count)];
                        Toggle(t, t.IsChecked != true);
                    }
                    if (!Complete()) { Report("Nach Änderung nicht wieder abschließbar", Describe(session)); break; }
                    AtEnd();
                    // Courses unrelated to the changed LF/oral subjects must survive the round trip.
                    var lfNow = session.Choice.Selections.Where(s => s.Type == CourseType.Lf).Select(s => s.SubjectId).ToHashSet();
                    var lost = completed.Selections.Where(s => s.Type != CourseType.Lf && !lfNow.Contains(s.SubjectId)
                        && !session.Choice.Selections.Any(n => n.SubjectId == s.SubjectId && s.Semesters.All(n.Semesters.Contains))
                        && !(s.SubjectId is "ETH" or "ER" or "KR")).Select(s => $"{s.OfferId}[{string.Join("", s.Semesters)}]").ToArray();
                    if (lost.Length > 0) Report("Kurse nach Rück-/Vorweg verloren", string.Join(",", lost) + $"\nVorher: {Selections(completed)}\nNachher: {Selections(session.Choice)}");
                }
            }
            catch (Exception) { }
            finally { if (session.IsDirty && !session.LoadFailed) await session.SaveAsync(); window.Close(); }
        }
        var report = Environment.GetEnvironmentVariable("GSM_MONKEY_REPORT");
        if (report is { Length: > 0 })
            File.AppendAllText(report, $"=== Rück-/Vorweg, Profil {profileName}: {findings.Count} Befundarten ===\n" + string.Join("\n", findings.Select(f => $"[{f.Key}]\n{f.Value}\n")) + "\n");
        if (Environment.GetEnvironmentVariable("GSM_MONKEY_EXPLORE") is not { Length: > 0 })
            Assert.True(findings.Count == 0, string.Join("\n\n", findings.Select(f => $"[{f.Key}]\n{f.Value}")));
        await Task.CompletedTask;
    }

    private static string Selections(CourseChoice c) =>
        string.Join(" ", c.Selections.Select(s => $"{s.OfferId}[{string.Join("", s.Semesters)}]")) + " mdl:" + string.Join("/", c.PlannedOralSubjects);

    private static string Describe(StudentSession session) => "Wahl: " + Selections(session.Choice);

    private sealed class MemoryStore : IChoiceStore
    {
        private StoredChoice? value;
        private int counter;
        public Task<StoredChoice?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<StoredChoice> SaveAsync(CourseChoice choice, string? expectedToken, CancellationToken cancellationToken = default)
        {
            if (value?.ContentToken != expectedToken) throw new IOException("Konflikt im Testspeicher.");
            value = new(choice, "token-" + ++counter);
            return Task.FromResult(value);
        }
    }
}
