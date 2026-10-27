using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public static class CatalogConfiguration
{
    // Produces a separate version before distribution. Never changes an administrative choice.
    public static OfferCatalog Prepare(OfferCatalog basis, int year, string version, IReadOnlySet<string> enabledLf)
    {
        if (string.IsNullOrWhiteSpace(version) || version == basis.Version) throw new ArgumentException("Neue eindeutige Angebotsversion erforderlich.");
        if (enabledLf.Any(id => !basis.Offers.Any(o => o.Id == id && o.Type == CourseType.Lf))) throw new ArgumentException("Unbekanntes LF-Angebot.");
        var catalog = basis with { GraduationYear = year, Version = version, Offers = [.. basis.Offers.Select(o => o.Type == CourseType.Lf ? o with { Status = enabledLf.Contains(o.Id) ? OfferStatus.Offered : OfferStatus.Planned } : o)] };
        ModelGuard.Catalog(catalog);
        var invalid = catalog.Offers.Where(o => o.Status == OfferStatus.Offered).SelectMany(o => CatalogRules.Check(o)).FirstOrDefault();
        if (invalid is not null) throw new ArgumentException(invalid.Rule.Id + ": " + invalid.Explanation);
        return catalog;
    }
}
