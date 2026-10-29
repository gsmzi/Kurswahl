using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

// One line of the permanent plan overview. OpenTaskId/missing rows are placeholders, never selections.
public sealed record PlanRow(string Label, string? OfferId, CourseType? Type, CourseVariant Variant,
    ImmutableArray<int> Semesters, bool IsAutomatic, bool IsOral, ReviewStatus? Issue, bool IsOpen, int Step, string? IssueText = null);

// Read-only view of one personal prerequisite. Values come only from the trusted profile, never from the choice file.
public sealed record PrerequisiteInfo(string Id, string Label, string Value, string Effect, bool IsVerified);

public sealed class StudentSession
{
    private readonly IChoiceStore store;
    private readonly ChoiceValidator validator = new();
    private string? contentToken;
    // Basic courses removed by choosing the subject as LF in this session, for restoring when the LF is given up.
    private sealed record DisplacedBasic(CourseSelection Basic, bool WasOral);
    private readonly Dictionary<string, DisplacedBasic> displacedBasics = [];
    private string? lfChangeSubject;
    public OfferCatalog Catalog { get; }
    public ProfileContext Profile { get; }
    public CourseChoice Choice { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsBusy { get; private set; }
    public bool LoadFailed { get; private set; }
    public string Message { get; private set; } = "Neuer vorläufiger Entwurf – noch nicht gespeichert.";
    public ValidationResult Validation => validator.Validate(Choice, Catalog, Profile);
    public ImmutableArray<SemesterSummary> Summary => ChoiceValidator.Summarize(Choice, Catalog);
    public event Action? Changed;
    public const int LastStep = 5;
    public int Step { get; private set; } = 1;
    public string? StepError => Step == 1 ? ChoiceWorkflow.LfError(Choice, Catalog, Profile)
        : Step == 2 ? ChoiceWorkflow.OralError(Choice, Catalog, Profile) : null;
    public ImmutableArray<string> OralCandidates => ChoiceWorkflow.OralCandidates(Choice, Catalog, Profile);
    public ImmutableArray<string> ForcedOral => ChoiceWorkflow.ForcedOral(Choice);
    public ImmutableArray<string> UsefulOralCandidates => ChoiceWorkflow.UsefulOralCandidates(Choice, Catalog, Profile);
    public ImmutableArray<string> PlanningNotes => ChoiceWorkflow.Remaining(Choice, Catalog, Profile);
    public ImmutableArray<ChoiceTask> Tasks => ChoiceWorkflow.Tasks(Choice, Catalog, Profile);
    public int CountedCourses => ChoiceWorkflow.CountedCourses(Choice);
    public ImmutableArray<ExamOption> ExamOptions => AcademicRules.ExamOptions(Choice, Catalog, Profile);
    public string AutomaticMessage { get; private set; } = "";
    public string LfChangeMessage { get; private set; } = "";

    public string ProfileStatusText => Profile.Status switch
    {
        ProfileStatus.Verified => $"Profil geprüft · Revision {Profile.Revision} · Stand {Profile.IssuedAt:dd.MM.yyyy HH:mm} · {Profile.Detail}",
        ProfileStatus.ProfileInvalid => "Profil ungültig (profile_invalid) – bitte an die Oberstufenberatung wenden. " + Profile.Detail,
        _ => "Kein geprüftes persönliches Profil – alle Angaben ungeprüft (needs_review). " + Profile.Detail
    };

    public ImmutableArray<PrerequisiteInfo> Prerequisites
    {
        get
        {
            var p = Profile.Prerequisites;
            var verified = Profile.Status == ProfileStatus.Verified;
            string Value(Knowledge k) => !verified ? "ungeprüft" : k switch { Knowledge.Yes => "ja", Knowledge.No => "nein", _ => "ungeprüft" };
            string Offers(Requirement requirement, string fallback)
            {
                var names = Catalog.Offers.Where(o => o.Requirements.Contains(requirement))
                    .Select(o => SubjectName(o.SubjectId) + (o.Type == CourseType.Lf ? " LF" : "") + (o.Variant == CourseVariant.Bilingual ? " bilingual" : ""))
                    .Distinct().ToArray();
                return names.Length == 0 ? fallback : "Nötig für: " + string.Join(", ", names) + ".";
            }
            return
            [
                new("INF", "Informatik: Brückenkurs Klasse 10 / IMP",
                    $"{Value(p.InformaticsBridge)} / {Value(p.InformaticsImp)}", Offers(Requirement.Informatics, "Im Angebot nicht vorhanden."), verified),
                new("I", "Italienisch als dritte Fremdsprache (Profil)",
                    verified ? $"{Value(p.ItalianProfile)}" + (p.ContinuedLanguages.IsEmpty ? "" : $" · bestätigte Sprachlaufbahn: {string.Join(", ", p.ContinuedLanguages)}") : "ungeprüft",
                    Offers(Requirement.ItalianProfile, "Im Angebot nicht vorhanden."), verified),
                new("BILI", "Durchgängig bilingualer Bildungsgang", Value(p.ContinuousBilingual), Offers(Requirement.ContinuousBilingual, "Im Angebot nicht vorhanden."), verified),
                new("REL", "Religion/Ethik in Klasse 10",
                    verified && p.ReligionEthicsGrade10 is { } subject ? $"{SubjectName(subject)} (Belegung/Prüfung: {Value(p.ReligionEthicsPermission)} / {Value(p.ReligionEthicsExamPermission)})" : "ungeprüft",
                    "Betrifft vor allem die mündliche Prüfung in Religion/Ethik (bei Wechsel Feststellungsprüfung nötig); die Basisfachwahl wird nicht pauschal gesperrt.", verified),
                new("SP", "Sportbefreiung",
                    verified && p.SportExemptSemesters is { } semesters ? (semesters.IsEmpty ? "keine" : "HJ " + string.Join(", ", semesters)) + $" (dauerhaft: {Value(p.PermanentSportExemption)})" : "ungeprüft",
                    "Bei Befreiung sind Ersatzkurse nötig und Sport entfällt als LF/mündliches Prüfungsfach; Prüfung durch die Oberstufenberatung.", verified)
            ];
        }
    }

    public string SubjectName(string subjectId) => Catalog.Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name ?? subjectId;

    public void PreviousStep()
    {
        if (IsBusy || Step == 1) return;
        Step--; Changed?.Invoke();
    }

    // Only completed steps can be revisited directly; moving forward always runs NextStep's checks.
    public void GoToStep(int step)
    {
        if (IsBusy || step < 1 || step >= Step) return;
        Step = step; Changed?.Invoke();
    }

    public ImmutableArray<int> LockedSemesters(string offerId)
    {
        var selection = Choice.Selections.FirstOrDefault(s => s.OfferId == offerId);
        if (selection is null) return [];
        if (selection.Type == CourseType.Lf) return selection.Semesters;
        var locked = ChoiceWorkflow.Required(Choice, Profile).Where(r => r.SubjectId == selection.SubjectId).SelectMany(r => r.Semesters);
        var task = Tasks.FirstOrDefault(t => t.ChosenOfferIds.Contains(offerId));
        if (task is not null) locked = locked.Concat(task.Semesters);
        return [.. locked.Distinct().Order()];
    }

    public bool IsRequiredSubject(string subjectId) => ChoiceWorkflow.Required(Choice, Profile).Any(r => r.SubjectId == subjectId);

    public ImmutableArray<ImmutableArray<int>> SemesterSelectionUnits(string offerId) =>
        AcademicRules.SemesterSelectionUnits(Choice, Catalog.Offers.Single(o => o.Id == offerId), Profile);

    public ImmutableArray<CourseOffer> LfOffers => [.. Catalog.Offers.Where(o => o.Type == CourseType.Lf && Availability(o).CanSelect)];

    public ImmutableArray<CourseOffer> ExtraOffers => [.. Catalog.Offers.Where(o => o.Type != CourseType.Lf && Availability(o).CanSelect
        && !Choice.Selections.Any(s => s.SubjectId == o.SubjectId || AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, o.SubjectId)))];

    public ImmutableArray<CourseOffer> RestrictedOffers(bool lf) => [.. Catalog.Offers.Where(o => (o.Type == CourseType.Lf) == lf
        && !Availability(o).CanSelect && !Choice.Selections.Any(s => s.OfferId == o.Id))];

    // All options of a task that are already selected for the whole task period, not only the one counted for it.
    public ImmutableArray<string> SelectedTaskOptions(ChoiceTask task) =>
        [.. task.OptionOfferIds.Union(task.ChosenOfferIds).Where(id => Choice.Selections.Any(s => s.OfferId == id && task.Semesters.All(s.Semesters.Contains)))];

    // A single answer is only replaced while it is the one visible answer. With several matching or
    // required courses nothing is removed implicitly; the student adds or removes courses explicitly.
    public bool ReplacesTaskAnswer(ChoiceTask task)
    {
        var selected = SelectedTaskOptions(task);
        return task.IsExclusive && selected.Length <= 1
            && !selected.Any(id => IsRequiredSubject(Catalog.Offers.Single(o => o.Id == id).SubjectId));
    }

    public void ChooseTaskOption(string taskId, string offerId, bool selected)
    {
        if (IsBusy) throw new InvalidOperationException("Dateivorgang läuft; bitte warten.");
        var task = Tasks.Single(t => t.Id == taskId);
        var offer = Catalog.Offers.Single(o => o.Id == offerId);
        if (!selected)
        {
            if (IsRequiredSubject(offer.SubjectId)) throw new InvalidOperationException("Dieses Fach ist durch deine Prüfungsplanung oder als Pflichtfach gesetzt.");
            SetSelection(offerId, []);
            return;
        }
        if (!task.OptionOfferIds.Contains(offerId)) throw new InvalidOperationException("Dieses Angebot passt nicht zu dieser Frage.");
        var existing = Choice.Selections.FirstOrDefault(s => s.OfferId == offerId)?.Semesters ?? [];
        // Already answers the task: choosing it again must not remove anything else.
        if (task.Semesters.All(existing.Contains)) return;
        var snapshot = Choice;
        try
        {
            if (ReplacesTaskAnswer(task))
                foreach (var previous in SelectedTaskOptions(task).Where(id => id != offerId)) SetSelection(previous, []);
            else if (!task.IsExclusive && task.ChosenOfferIds.Length >= task.Choose)
                throw new InvalidOperationException("Zuerst eine gewählte Option abwählen oder weitere Kurse in Schritt 4 ergänzen.");
            SetSelection(offerId, existing.Union(task.Semesters));
        }
        catch (InvalidOperationException)
        {
            // Never leave a half-replaced answer behind.
            if (Choice != snapshot) { Choice = snapshot; Changed?.Invoke(); }
            throw;
        }
    }

    public ImmutableArray<PlanRow> Plan
    {
        get
        {
            var tasks = Tasks;
            var required = ChoiceWorkflow.Required(Choice, Profile);
            var messages = Validation.Messages;
            (ReviewStatus? Status, string? Text) Issue(string subject)
            {
                var relevant = messages.Where(m => m.SubjectIds.Contains(subject)).ToArray();
                var status = relevant.Any(m => m.Rule.Severity == ReviewStatus.Invalid) ? ReviewStatus.Invalid
                    : relevant.Length > 0 ? ReviewStatus.NeedsReview : (ReviewStatus?)null;
                var text = relevant.Length == 0 ? null : string.Join("\n\n", relevant.Select(m => $"{m.Rule.Id}: {m.Explanation}\n{m.Remedy}"));
                return (status, text);
            }
            int Order(CourseSelection s) => s.Type == CourseType.Lf ? 0
                : required.Any(r => r.SubjectId == s.SubjectId) ? 1 : tasks.Any(t => t.ChosenOfferIds.Contains(s.OfferId)) ? 2 : 3;
            var rows = Choice.Selections.OrderBy(Order).ThenBy(s => Catalog.Offers.ToList().FindIndex(o => o.Id == s.OfferId))
                .Select(s =>
                {
                    var issue = Issue(s.SubjectId);
                    return new PlanRow(SubjectName(s.SubjectId), s.OfferId, s.Type, s.Variant, s.Semesters,
                        Order(s) == 1, Choice.PlannedOralSubjects.Contains(s.SubjectId), issue.Status, false,
                        Order(s) switch { 0 => 1, 3 => 4, _ => 3 }, issue.Text);
                }).ToList();
            if (Step >= 3)
                rows.AddRange(required.Where(r => !Choice.Selections.Any(s => s.SubjectId == r.SubjectId))
                    .Select(r => new PlanRow(SubjectName(r.SubjectId) + " – Angebot klären", null, null, CourseVariant.Normal, r.Semesters, true, false, ReviewStatus.NeedsReview, true, 3,
                        $"PLAN-REQUIRED: {SubjectName(r.SubjectId)} benötigt HJ {string.Join(", ", r.Semesters)}; kein eindeutiges verfügbares BF im Angebot.")));
            rows.AddRange(tasks.Where(t => !t.IsSatisfied).Select(t => new PlanRow(t.Title, null, null, CourseVariant.Normal, t.Semesters, false, false, null, true, 3, t.Hint)));
            return [.. rows];
        }
    }

    public void NextStep()
    {
        if (IsBusy) throw new InvalidOperationException("Dateivorgang läuft; bitte warten.");
        if (StepError is { } error) throw new InvalidOperationException(error);
        if (Step == 1)
        {
            var forced = ForcedOral;
            var oral = forced.Concat(Choice.PlannedOralSubjects.Where(s => OralCandidates.Contains(s))).Distinct().Take(2).ToImmutableArray();
            if (oral.Length != Choice.PlannedOralSubjects.Length || oral.Any(s => !Choice.PlannedOralSubjects.Contains(s)))
            {
                Choice = Choice with { PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow };
                IsDirty = true;
            }
        }
        if (Step == 2) { ApplyRequired(); LfChangeMessage = ""; lfChangeSubject = null; }
        Step = Math.Min(LastStep, Step + 1);
        if (IsDirty) Message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        Changed?.Invoke();
    }

    public void SetOral(string subject, bool selected)
    {
        if (IsBusy || Step != 2) throw new InvalidOperationException("Mündliche Planung ist in Schritt 2 möglich.");
        if (!selected && ForcedOral.Contains(subject)) throw new InvalidOperationException("Deutsch/Mathematik ist ohne entsprechendes LF mündlich erforderlich.");
        var oral = Choice.PlannedOralSubjects.Remove(subject);
        if (selected)
        {
            if (!OralCandidates.Contains(subject)) throw new InvalidOperationException("Fach ist für diese Planung nicht verfügbar.");
            if (oral.Length == 2) throw new InvalidOperationException("Zuerst ein mündliches Fach abwählen.");
            oral = oral.Add(subject);
        }
        Choice = Choice with { PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow };
        IsDirty = true;
        Message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        Changed?.Invoke();
    }

    private void ApplyRequired()
    {
        var selections = Choice.Selections;
        var notes = new List<string>();
        foreach (var required in ChoiceWorkflow.Required(Choice, Profile))
        {
            var existing = selections.FirstOrDefault(s => s.SubjectId == required.SubjectId);
            var offers = Catalog.Offers.Where(o => o.SubjectId == required.SubjectId && o.Type == CourseType.Bf
                && Availability(o).CanSelect && required.Semesters.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
            var offer = existing is null ? (offers.Length == 1 ? offers[0] : null) : offers.FirstOrDefault(o => o.Id == existing.OfferId);
            if (offer is null) { notes.Add($"{SubjectName(required.SubjectId)} ({required.SubjectId}): needs_review – kein eindeutiges verfügbares BF; vorhandene Wahl bleibt erhalten."); continue; }
            var semesters = (existing?.Semesters ?? []).Union(required.Semesters).Order().ToImmutableArray();
            var replaced = selections.Where(s => AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, required.SubjectId)
                && s.Semesters.Intersect(semesters).Any()).ToArray();
            foreach (var previous in replaced)
                notes.Add($"{SubjectName(previous.SubjectId)} wurde durch {SubjectName(required.SubjectId)} ersetzt, weil du es mündlich planst. Religion und Ethik können nicht gleichzeitig belegt werden.");
            selections = selections.Except(replaced).ToImmutableArray();
            if (existing is not null && existing.Semesters.SequenceEqual(semesters)) continue;
            selections = selections.Where(s => s.OfferId != offer.Id).ToImmutableArray()
                .Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, semesters));
        }
        if (!selections.SequenceEqual(Choice.Selections))
        {
            Choice = Choice with { Selections = selections, UpdatedAt = DateTimeOffset.UtcNow };
            IsDirty = true;
        }
        // Successful additions are visible in the plan; only problems need words.
        AutomaticMessage = string.Join("\n", notes);
    }

    public StudentSession(OfferCatalog catalog, string demoStudentId, IChoiceStore store, ProfileContext? profile = null)
    {
        ModelGuard.Catalog(catalog);
        if (!catalog.IsDemo || !demoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            throw new ArgumentException("Der Demonstrator unterstützt nur ausdrücklich synthetische Demoangebote und demo-Schüler-IDs.");
        Catalog = catalog;
        Profile = profile ?? ProfileContext.Missing;
        this.store = store;
        var now = DateTimeOffset.UtcNow;
        Choice = new(2, Guid.NewGuid(), demoStudentId, catalog.GraduationYear, catalog.Version, ChoiceValidator.Version,
            1, now, now, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
    }

    public Availability Availability(CourseOffer offer) => validator.GetAvailability(offer, Profile);

    public void SetSelection(string offerId, IEnumerable<int> semesters)
    {
        if (IsBusy) throw new InvalidOperationException("Dateivorgang läuft; bitte warten.");
        var offer = Catalog.Offers.Single(o => o.Id == offerId);
        var selected = semesters.Order().ToImmutableArray();
        if (!selected.IsEmpty)
        {
            var availability = Availability(offer);
            if (!availability.CanSelect) throw new InvalidOperationException(availability.Explanation);
            if (selected.Distinct().Count() != selected.Length || selected.Any(s => !offer.Schedule.Any(h => h.Semester == s)))
                throw new InvalidOperationException("Halbjahr wird für dieses Angebot nicht angeboten.");
            if (offer.Type == CourseType.Lf && selected.Length != 4)
                throw new InvalidOperationException("Leistungsfächer werden für alle vier Halbjahre gewählt.");
            if (offer.Type == CourseType.Lf && Choice.Selections.Count(s => s.Type == CourseType.Lf && s.OfferId != offerId) >= 3)
                throw new InvalidOperationException("Es sind bereits drei Leistungsfächer gewählt. Zum Tauschen zuerst ein Leistungsfach abwählen.");
            if (offer.Type == CourseType.Seminar && !selected.SequenceEqual(offer.Schedule.Select(h => h.Semester).Order()))
                throw new InvalidOperationException("Seminarkurse werden über alle angebotenen Halbjahre gewählt.");
            if (Choice.Selections.Any(s => AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, offer.SubjectId)
                && s.Semesters.Intersect(selected).Any()))
                throw new InvalidOperationException("Religion und Ethik sowie verschiedene Religionsfächer können nicht gleichzeitig belegt werden. Wechsle das Fach im Schritt Pflichtbereiche; ein mündlich geplantes Fach zuerst in Prüfungsfächer ändern.");
            if (Choice.Selections.Any(s => s.OfferId != offerId && s.SubjectId == offer.SubjectId
                && !(offer.Type == CourseType.Lf && s.Type == CourseType.Bf)))
                throw new InvalidOperationException("Dieses Fach ist bereits gewählt. Vor einem Wechsel die bisherige Kursart/Variante abwählen.");
        }
        var replacedBasic = !selected.IsEmpty && offer.Type == CourseType.Lf
            ? Choice.Selections.FirstOrDefault(s => s.SubjectId == offer.SubjectId && s.Type == CourseType.Bf) : null;
        var replacesBasic = replacedBasic is not null;
        // Giving up an LF again restores the basic course it had replaced, if that is still possible.
        var removesLf = selected.IsEmpty && offer.Type == CourseType.Lf && Choice.Selections.Any(s => s.OfferId == offerId);
        DisplacedBasic? restored = null;
        if (removesLf && displacedBasics.Remove(offer.SubjectId, out var displaced)
            && Catalog.Offers.FirstOrDefault(o => o.Id == displaced.Basic.OfferId) is { } basicOffer && Availability(basicOffer).CanSelect
            && !Choice.Selections.Any(s => s.OfferId != offerId && (s.SubjectId == offer.SubjectId
                || (AcademicRules.MutuallyExclusiveSubjects(s.SubjectId, offer.SubjectId) && s.Semesters.Intersect(displaced.Basic.Semesters).Any()))))
            restored = displaced;
        var selections = Choice.Selections.Where(s => s.OfferId != offerId
            && !(replacesBasic && s.SubjectId == offer.SubjectId && s.Type == CourseType.Bf)).ToImmutableArray();
        if (!selected.IsEmpty) selections = selections.Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, selected));
        if (restored is not null) selections = selections.Add(restored.Basic);
        var oral = !selected.IsEmpty && offer.Type == CourseType.Lf
            ? Choice.PlannedOralSubjects.Remove(offer.SubjectId)
            : Choice.PlannedOralSubjects;
        if (restored is { WasOral: true } && oral.Length < 2 && !oral.Contains(offer.SubjectId)) oral = oral.Add(offer.SubjectId);
        if (replacedBasic is not null) displacedBasics[offer.SubjectId] = new(replacedBasic, Choice.PlannedOralSubjects.Contains(offer.SubjectId));
        else if (!selected.IsEmpty && offer.Type != CourseType.Lf) displacedBasics.Remove(offer.SubjectId);
        Choice = Choice with { Selections = selections, PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow };
        if (replacesBasic)
        {
            LfChangeMessage = $"{SubjectName(offer.SubjectId)} ist jetzt Leistungsfach. Die bisherige Basisfachbelegung wurde automatisch entfernt. Bitte die Prüfungsfächer und die weiteren Schritte erneut durchgehen.";
            lfChangeSubject = offer.SubjectId;
        }
        else if (restored is not null)
        {
            LfChangeMessage = $"{SubjectName(offer.SubjectId)} ist kein Leistungsfach mehr. Die vorherige Basisfachbelegung (HJ {string.Join(", ", restored.Basic.Semesters)}) wurde wiederhergestellt"
                + (oral.Contains(offer.SubjectId) && restored.WasOral ? ", ebenso die mündliche Planung" : "") + ". Bitte die Prüfungsfächer und die weiteren Schritte erneut durchgehen.";
            lfChangeSubject = offer.SubjectId;
        }
        else if (removesLf && lfChangeSubject == offer.SubjectId) { LfChangeMessage = ""; lfChangeSubject = null; }
        IsDirty = true;
        Message = "Ungespeicherte Änderungen – vorläufig, nicht eingereicht.";
        Changed?.Invoke();
    }

    public async Task LoadAsync(bool discardChanges = false, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        if (IsDirty && !discardChanges) throw new InvalidOperationException("Ungespeicherte Änderungen: Verwerfen muss bestätigt werden.");
        IsBusy = true; Changed?.Invoke();
        try
        {
            var loaded = await store.LoadAsync(cancellationToken);
            if (loaded is null)
            {
                if (contentToken is not null || IsDirty)
                {
                    // Keep the open draft. Without a token the next save may only create the file, never replace one.
                    contentToken = null;
                    IsDirty = true;
                    Message = "Gespeicherte Datei nicht gefunden. Entwurf bleibt erhalten und wird beim nächsten Speichern neu angelegt.";
                }
                else Message = "Keine gespeicherte Wahl vorhanden. Neuer vorläufiger Entwurf.";
            }
            else
            {
                var candidate = loaded.Choice;
                ModelGuard.Choice(candidate);
                if (candidate.StudentId != Choice.StudentId || candidate.GraduationYear != Catalog.GraduationYear
                    || candidate.OfferVersion != Catalog.Version || candidate.RuleVersion != ChoiceValidator.Version
                    || candidate.SubmissionStatus != SubmissionStatus.Draft || candidate.Phase != ElectionPhase.Preliminary)
                    throw new InvalidOperationException("Datei gehört zu anderer Demo-ID, anderem Jahrgang/Versionsstand oder ist kein unterstützter Vorwahlentwurf. Bestand bleibt erhalten.");
                // Preserve even now-invalid selections; never silently discard unknown or restricted offers.
                Choice = candidate;
                Step = 1;
                AutomaticMessage = "";
                LfChangeMessage = "";
                lfChangeSubject = null;
                displacedBasics.Clear();
                contentToken = loaded.ContentToken;
                IsDirty = false;
                Message = $"Entwurf geladen (Revision {Choice.Revision}). Weiterhin fachlich zu klären.";
            }
            LoadFailed = false;
        }
        catch
        {
            LoadFailed = true;
            Message = "Laden fehlgeschlagen. Entwurf erhalten; Speichern bis zum erfolgreichen Laden gesperrt.";
            throw;
        }
        finally { IsBusy = false; Changed?.Invoke(); }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        if (LoadFailed) throw new InvalidOperationException("Zuerst den Ladefehler klären; vorhandene Datei wird geschützt.");
        if (!IsDirty && contentToken is not null) return;
        IsBusy = true; Changed?.Invoke();
        try
        {
            var candidate = Choice with { Revision = contentToken is null ? Choice.Revision : Choice.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow };
            var saved = await store.SaveAsync(candidate, contentToken, cancellationToken);
            Choice = saved.Choice; contentToken = saved.ContentToken; IsDirty = false;
            Message = $"Entwurf gespeichert (Revision {Choice.Revision}) – nicht eingereicht, fachlich zu klären.";
        }
        catch { Message = "Speichern fehlgeschlagen. Ungespeicherter Entwurf bleibt geöffnet."; throw; }
        finally { IsBusy = false; Changed?.Invoke(); }
    }
}
