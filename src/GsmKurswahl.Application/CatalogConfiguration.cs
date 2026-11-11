using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public static class CatalogConfiguration
{
    public static OfferCatalog Annotate(OfferCatalog basis, string offerId, CooperationSchool school, bool topUp)
    {
        if (!Enum.IsDefined(school) || !basis.Offers.Any(o => o.Id == offerId)) throw new ArgumentException("Unbekanntes Angebot oder Koopkurs-Standort.");
        // Keep the offer version: annotations do not require pupils to reselect or invalidate their drafts.
        var next = basis with { Offers = [.. basis.Offers.Select(o => o.Id == offerId ? o with { CooperationSchool = school, IsTopUpCourse = topUp } : o)] };
        ModelGuard.Catalog(next);
        return next;
    }
    // Produces a separate version before distribution. Never changes an administrative choice.
    public static OfferCatalog Prepare(OfferCatalog basis, int year, string version, IReadOnlySet<string> enabledLf, IReadOnlySet<string>? enabledOptional = null)
    {
        if (string.IsNullOrWhiteSpace(version) || version == basis.Version) throw new ArgumentException("Neue eindeutige Angebotsversion erforderlich.");
        if (enabledLf.Any(id => !basis.Offers.Any(o => o.Id == id && o.Type == CourseType.Lf))) throw new ArgumentException("Unbekanntes LF-Angebot.");
        if (enabledOptional is not null && enabledOptional.Any(id => !basis.Offers.Any(o => o.Id == id && o.Type != CourseType.Lf && PlanningPresentation.CanCancel(o))))
            throw new ArgumentException("Unbekanntes oder verpflichtendes Basis-/Wahlfachangebot.");
        var catalog = basis with
        {
            GraduationYear = year,
            Version = version,
            Offers = [.. basis.Offers.Select(o => o.Type == CourseType.Lf
                ? o with { Status = enabledLf.Contains(o.Id) ? OfferStatus.Offered : OfferStatus.Planned }
                : enabledOptional is not null && PlanningPresentation.CanCancel(o)
                    ? o with { Status = enabledOptional.Contains(o.Id) ? OfferStatus.Offered : OfferStatus.Planned }
                    : o)]
        };
        ModelGuard.Catalog(catalog);
        var invalid = catalog.Offers.Where(o => o.Status == OfferStatus.Offered).SelectMany(o => CatalogRules.Check(o)).FirstOrDefault();
        if (invalid is not null) throw new ArgumentException(invalid.Rule.Id + ": " + invalid.Explanation);
        return catalog;
    }
}
