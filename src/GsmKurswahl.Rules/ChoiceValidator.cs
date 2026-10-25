using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Rules;

public sealed record Availability(bool CanSelect, ReviewStatus Status, string Explanation);

public sealed class ChoiceValidator
{
    public const string Version = "m1-1";
    public static readonly ImmutableArray<RuleDefinition> Definitions =
    [
        new("M1-COMPLETENESS", "Unvollständige fachliche Prüfung", "Pflichtenheft §§6,17 (M2)", "Alle M1-Wahlen", Version, ReviewStatus.NeedsReview),
        new("PROFILE-TRUST", "Vertrauenswürdiges Einzelprofil fehlt", "Pflichtenheft §5a", "Alle Schülerwahlen", Version, ReviewStatus.NeedsReview),
        new("OFFER-VERSION", "Jahrgang und Angebots-/Regelversion", "Pflichtenheft §§4,8", "Alle Wahlen", Version, ReviewStatus.Invalid),
        new("OFFER-SELECTION", "Angebot, Variante und Halbjahre", "Pflichtenheft §§4,8", "Jede Belegung", Version, ReviewStatus.Invalid),
        new("SUBJECT-DUPLICATE", "Keine doppelte Fachbelegung", "Pflichtenheft §6 Nr.1,7", "Alle Fächer/Varianten", Version, ReviewStatus.Invalid),
        new("PREREQUISITE-NO", "Persönliche Voraussetzung nicht erfüllt", "Pflichtenheft §§4,5", "INF/I/bilingual", Version, ReviewStatus.Invalid),
        new("PREREQUISITE-UNKNOWN", "Persönliche Voraussetzung ungeklärt", "Pflichtenheft §§5,5a", "INF/I/bilingual", Version, ReviewStatus.NeedsReview)
    ];

    public Availability GetAvailability(CourseOffer offer, ProfileContext profile)
    {
        if (offer.Status is not (OfferStatus.Offered or OfferStatus.Established))
            return new(false, ReviewStatus.Invalid, "Dieses Angebot ist geplant oder nicht zustande gekommen und nicht freigegeben.");
        if (offer.Requirements.IsEmpty) return new(true, ReviewStatus.NeedsReview, "Vorläufig wählbar; vollständige Rechtsprüfung folgt in M2.");
        if (profile.Status != ProfileStatus.Verified)
            return new(false, ReviewStatus.NeedsReview, "Persönliche Voraussetzung ungeprüft. Bitte an die Oberstufenberatung wenden.");
        var states = offer.Requirements.Select(r => r switch
        {
            Requirement.ItalianProfile => profile.Prerequisites.ItalianProfile,
            Requirement.ContinuousBilingual => profile.Prerequisites.ContinuousBilingual,
            Requirement.Informatics => Either(profile.Prerequisites.InformaticsBridge, profile.Prerequisites.InformaticsImp),
            _ => Knowledge.Unknown
        }).ToArray();
        if (states.Contains(Knowledge.No)) return new(false, ReviewStatus.Invalid, "Persönliche Voraussetzung nicht erfüllt.");
        if (states.Contains(Knowledge.Unknown)) return new(false, ReviewStatus.NeedsReview, "Voraussetzung fachlich zu klären; Oberstufenberatung kontaktieren.");
        return new(true, ReviewStatus.NeedsReview, "Vorbildung erfüllt; vollständige Rechtsprüfung steht aus.");
    }

    public ValidationResult Validate(CourseChoice choice, OfferCatalog catalog, ProfileContext profile)
    {
        ModelGuard.Choice(choice);
        ModelGuard.Catalog(catalog);
        var messages = ImmutableArray.CreateBuilder<RuleMessage>();
        void Add(string id, string explanation, string remedy, string? subject = null, ImmutableArray<int> semesters = default) =>
            messages.Add(new(Definitions.Single(d => d.Id == id), subject is null ? [] : [subject], semesters.IsDefault ? [] : semesters, explanation, remedy));
        Add("M1-COMPLETENESS", "Fachlich zu klären (needs_review): M1 prüft noch keine vollständige AGVO-Belegung oder Prüfungsfachkombination.", "Nur als Entwurf verwenden; Regelprüfung in M2 und schulische Freigabe abwarten.");
        if (profile.Status != ProfileStatus.Verified)
            Add("PROFILE-TRUST", "Kein verifiziertes persönliches Profil; Profilstatus: " + profile.Status, "Oberstufenberatung kontaktieren. M1 arbeitet ausschließlich mit synthetischen Entwürfen.");
        if (choice.GraduationYear != catalog.GraduationYear || choice.OfferVersion != catalog.Version || choice.RuleVersion != Version)
            Add("OFFER-VERSION", "Jahrgang, Angebotsversion oder Regelversion weicht ab.", "Vorhandenen Entwurf erhalten und Version durch Oberstufenberatung klären lassen.");
        foreach (var selection in choice.Selections)
        {
            var offer = catalog.Offers.SingleOrDefault(o => o.Id == selection.OfferId);
            if (offer is null || offer.SubjectId != selection.SubjectId || offer.Type != selection.Type || offer.Variant != selection.Variant
                || selection.Semesters.Any(s => !offer.Schedule.Any(h => h.Semester == s))
                || (offer.Type == CourseType.Lf && selection.Semesters.Length != 4))
            {
                Add("OFFER-SELECTION", "Belegung passt nicht zum konfigurierten Angebot.", "Fach, Kursart, Variante und Halbjahre kontrollieren.", selection.SubjectId, selection.Semesters);
                continue;
            }
            var availability = GetAvailability(offer, profile);
            if (!availability.CanSelect)
                Add(offer.Status is not (OfferStatus.Offered or OfferStatus.Established) ? "OFFER-SELECTION" : availability.Status == ReviewStatus.Invalid ? "PREREQUISITE-NO" : "PREREQUISITE-UNKNOWN",
                    availability.Explanation, "Angebot/Vorbildung mit der Oberstufenberatung klären.", selection.SubjectId, selection.Semesters);
        }
        foreach (var duplicate in choice.Selections.GroupBy(s => s.SubjectId).Where(g => g.Count() > 1))
            Add("SUBJECT-DUPLICATE", "Dasselbe Fach wurde mehrfach als Kursart oder Variante gewählt.", "Eine eindeutige Fachbelegung wählen; Sonderfälle sind in M1 nicht freigegeben.", duplicate.Key);
        return new(messages.Any(m => m.Rule.Severity == ReviewStatus.Invalid) ? ReviewStatus.Invalid : ReviewStatus.NeedsReview, messages.ToImmutable());
    }

    public static ImmutableArray<SemesterSummary> Summarize(CourseChoice choice, OfferCatalog catalog) =>
        Enumerable.Range(1, 4).Select(semester =>
        {
            var selected = choice.Selections.Where(s => s.Semesters.Contains(semester)).ToArray();
            return new SemesterSummary(semester, selected.Length, selected.Sum(s => catalog.Offers.FirstOrDefault(o => o.Id == s.OfferId)?.Schedule.FirstOrDefault(h => h.Semester == semester)?.WeeklyHours ?? 0));
        }).ToImmutableArray();

    private static Knowledge Either(Knowledge a, Knowledge b) => a == Knowledge.Yes || b == Knowledge.Yes
        ? Knowledge.Yes : a == Knowledge.No && b == Knowledge.No ? Knowledge.No : Knowledge.Unknown;
}
