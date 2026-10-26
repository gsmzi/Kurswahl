namespace GsmKurswahl.Domain;

public static class ModelGuard
{
    public static void Choice(CourseChoice value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Require(value.SchemaVersion == 2, "Nicht unterstützte Wahl-Schemaversion. Version 2 erforderlich; keine automatische Migration.");
        Require(!value.PlannedOralSubjects.IsDefault && value.PlannedOralSubjects.Length <= 2
            && value.PlannedOralSubjects.All(s => !string.IsNullOrWhiteSpace(s))
            && value.PlannedOralSubjects.Distinct().Count() == value.PlannedOralSubjects.Length, "Ungültige mündliche Prüfungsplanung.");
        Require(value.DocumentId != Guid.Empty && !string.IsNullOrWhiteSpace(value.StudentId), "Wahl-/Schüler-ID fehlt.");
        Require(value.GraduationYear is >= 2000 and <= 2200, "Ungültiger Abiturjahrgang.");
        Require(!string.IsNullOrWhiteSpace(value.OfferVersion) && !string.IsNullOrWhiteSpace(value.RuleVersion), "Versionsangaben fehlen.");
        Require(value.Revision > 0 && value.CreatedAt != default && value.UpdatedAt >= value.CreatedAt, "Ungültige Revision/Zeitangaben.");
        Require(Enum.IsDefined(value.SubmissionStatus) && Enum.IsDefined(value.Phase), "Unbekannter Wahlstatus.");
        Require(!value.Selections.IsDefault, "Belegungen fehlen.");
        foreach (var selection in value.Selections)
        {
            Require(selection is not null, "Leere Belegung.");
            Require(!string.IsNullOrWhiteSpace(selection!.OfferId) && !string.IsNullOrWhiteSpace(selection.SubjectId), "Angebots-/Fachkennung fehlt.");
            Require(Enum.IsDefined(selection.Type) && Enum.IsDefined(selection.Variant), "Unbekannte Kursart/Variante.");
            Require(!selection.Semesters.IsDefaultOrEmpty && selection.Semesters.All(h => h is >= 1 and <= 4)
                && selection.Semesters.Distinct().Count() == selection.Semesters.Length, "Ungültige Halbjahre.");
        }
        Require(value.Selections.Select(s => s.OfferId).Distinct().Count() == value.Selections.Length, "Doppelte Angebotskennung.");
    }

    public static void Catalog(OfferCatalog value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Require(value.SchemaVersion == 1, "Nicht unterstützte Katalog-Schemaversion.");
        Require(!string.IsNullOrWhiteSpace(value.Version) && value.GraduationYear is >= 2000 and <= 2200, "Katalogversion/Jahrgang ungültig.");
        Require(!value.Subjects.IsDefaultOrEmpty && !value.Offers.IsDefaultOrEmpty, "Leerer Katalog.");
        Require(value.Subjects.All(s => s is not null && !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name)), "Ungültiges Fach.");
        Require(value.Subjects.Select(s => s.Id).Distinct().Count() == value.Subjects.Length, "Doppelte Fachkennung.");
        Require(value.Offers.All(o => o is not null && !string.IsNullOrWhiteSpace(o.Id)), "Ungültiges Angebot.");
        Require(value.Offers.Select(o => o.Id).Distinct().Count() == value.Offers.Length, "Doppelte Angebotskennung.");
        foreach (var offer in value.Offers)
        {
            Require(value.Subjects.Any(s => s.Id == offer.SubjectId), "Unbekannte Fachkennung im Angebot.");
            Require(Enum.IsDefined(offer.Type) && Enum.IsDefined(offer.Variant) && Enum.IsDefined(offer.Status), "Unbekannte Angebotsoption.");
            Require(!offer.Schedule.IsDefaultOrEmpty && offer.Schedule.All(s => s is not null && s.Semester is >= 1 and <= 4 && s.WeeklyHours is > 0 and <= 10)
                && offer.Schedule.Select(s => s.Semester).Distinct().Count() == offer.Schedule.Length, "Ungültiger Halbjahresplan.");
            Require(!offer.Requirements.IsDefault && offer.Requirements.All(Enum.IsDefined), "Unbekannte Voraussetzung.");
            // Schulische Angebotsgrenzen (LF-Ausschlüsse, PSY, SIA, GEO, INF, I, bilingual) prüft GsmKurswahl.Rules.CatalogRules.
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}
