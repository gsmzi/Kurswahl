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

    public StudentSession(OfferCatalog catalog, string demoStudentId, IChoiceStore store)
    {
        ModelGuard.Catalog(catalog);
        if (!catalog.IsDemo || !demoStudentId.StartsWith("demo-", StringComparison.Ordinal))
            throw new ArgumentException("M1 unterstützt nur ausdrücklich synthetische Demoangebote und demo-Schüler-IDs.");
        Catalog = catalog;
        this.store = store;
        var now = DateTimeOffset.UtcNow;
        Choice = new(1, Guid.NewGuid(), demoStudentId, catalog.GraduationYear, catalog.Version, ChoiceValidator.Version,
            1, now, now, SubmissionStatus.Draft, ElectionPhase.Preliminary, []);
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
                if (contentToken is not null || IsDirty) throw new IOException("Die gespeicherte Datei fehlt. Vorhandener Entwurf bleibt erhalten.");
                Message = "Keine gespeicherte Wahl vorhanden. Neuer vorläufiger Entwurf.";
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
