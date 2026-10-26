using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed class StudentSession
{
    private readonly IChoiceStore store;
    private readonly ChoiceValidator validator = new();
    private string? contentToken;
    public OfferCatalog Catalog { get; }
    public CourseChoice Choice { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsBusy { get; private set; }
    public bool LoadFailed { get; private set; }
    public string Message { get; private set; } = "Neuer vorläufiger Entwurf – noch nicht gespeichert.";
    public ValidationResult Validation => validator.Validate(Choice, Catalog, ProfileContext.Missing);
    public ImmutableArray<SemesterSummary> Summary => ChoiceValidator.Summarize(Choice, Catalog);
    public event Action? Changed;
    public int Step { get; private set; } = 1;
    public string? StepError => Step == 1 ? ChoiceWorkflow.LfError(Choice, Catalog)
        : Step == 2 ? ChoiceWorkflow.OralError(Choice, Catalog) : null;
    public ImmutableArray<string> OralCandidates => ChoiceWorkflow.OralCandidates(Choice, Catalog);
    public ImmutableArray<string> ForcedOral => ChoiceWorkflow.ForcedOral(Choice);
    public ImmutableArray<string> PlanningNotes => ChoiceWorkflow.Remaining(Choice);
    public string AutomaticMessage { get; private set; } = "";

    public void PreviousStep()
    {
        if (IsBusy || Step == 1) return;
        Step--; Changed?.Invoke();
    }

    public void NextStep()
    {
        if (IsBusy) throw new InvalidOperationException("Dateivorgang läuft; bitte warten.");
        if (StepError is { } error) throw new InvalidOperationException(error);
        if (Step == 1)
        {
            var forced = ForcedOral;
            var oral = forced.Concat(Choice.PlannedOralSubjects.Where(s => OralCandidates.Contains(s))).Distinct().Take(2).ToImmutableArray();
            if (!oral.SequenceEqual(Choice.PlannedOralSubjects))
            {
                Choice = Choice with { PlannedOralSubjects = oral, UpdatedAt = DateTimeOffset.UtcNow };
                IsDirty = true;
            }
        }
        if (Step == 2) ApplyRequired();
        Step = Math.Min(4, Step + 1);
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
        foreach (var required in ChoiceWorkflow.Required(Choice))
        {
            var existing = selections.FirstOrDefault(s => s.SubjectId == required.SubjectId);
            var offers = Catalog.Offers.Where(o => o.SubjectId == required.SubjectId && o.Type == CourseType.Bf
                && Availability(o).CanSelect && required.Semesters.All(h => o.Schedule.Any(s => s.Semester == h))).ToArray();
            var offer = existing is null ? (offers.Length == 1 ? offers[0] : null) : offers.FirstOrDefault(o => o.Id == existing.OfferId);
            if (offer is null) { notes.Add($"{required.SubjectId}: needs_review – kein eindeutiges verfügbares BF; vorhandene Wahl bleibt erhalten."); continue; }
            var semesters = (existing?.Semesters ?? []).Union(required.Semesters).Order().ToImmutableArray();
            if (existing is not null && existing.Semesters.SequenceEqual(semesters)) continue;
            selections = selections.Where(s => s.OfferId != offer.Id).ToImmutableArray()
                .Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, semesters));
            notes.Add($"{required.SubjectId}: HJ {string.Join(", ", semesters)} automatisch ergänzt.");
        }
        if (!selections.SequenceEqual(Choice.Selections))
        {
            Choice = Choice with { Selections = selections, UpdatedAt = DateTimeOffset.UtcNow };
            IsDirty = true;
        }
        AutomaticMessage = string.Join("\n", notes.Prepend("Pflichtbasisfächer und mündliche Planungsfächer ergänzt. Bestehende zusätzliche Kurse bleiben erhalten."));
    }

    public StudentSession(OfferCatalog catalog, string demoStudentId, IChoiceStore store)
    {
        ModelGuard.Catalog(catalog);
        if (!catalog.IsDemo || !demoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            throw new ArgumentException("M1 unterstützt nur ausdrücklich synthetische Demoangebote und demo-Schüler-IDs.");
        Catalog = catalog;
        this.store = store;
        var now = DateTimeOffset.UtcNow;
        Choice = new(2, Guid.NewGuid(), demoStudentId, catalog.GraduationYear, catalog.Version, ChoiceValidator.Version,
            1, now, now, SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
    }

    public Availability Availability(CourseOffer offer) => validator.GetAvailability(offer, ProfileContext.Missing);

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
            if (Choice.Selections.Any(s => s.OfferId != offerId && s.SubjectId == offer.SubjectId))
                throw new InvalidOperationException("Dieses Fach ist bereits gewählt. Vor einem Wechsel die bisherige Kursart/Variante abwählen.");
        }
        var selections = Choice.Selections.Where(s => s.OfferId != offerId).ToImmutableArray();
        if (!selected.IsEmpty) selections = selections.Add(new(offer.Id, offer.SubjectId, offer.Type, offer.Variant, selected));
        Choice = Choice with { Selections = selections, UpdatedAt = DateTimeOffset.UtcNow };
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
                    throw new InvalidOperationException("Datei gehört zu anderer Demo-ID, anderem Jahrgang/Versionsstand oder ist kein M1-Vorwahlentwurf. Bestand bleibt erhalten.");
                // Preserve even now-invalid selections; never silently discard unknown or restricted offers.
                Choice = candidate;
                Step = 1;
                AutomaticMessage = "";
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
