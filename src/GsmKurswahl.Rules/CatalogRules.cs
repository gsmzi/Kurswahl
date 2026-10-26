using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Rules;

// GSM-Angebotsgrenzen aus dem Pflichtenheft. Ein Verstoß sperrt das betroffene Angebot, nicht den ganzen Katalog.
public static class CatalogRules
{
    private const string Remedy = "Angebotskonfiguration durch die Oberstufenberatung korrigieren lassen; Angebot bis dahin gesperrt.";
    private static readonly string[] ExcludedLfSubjects = ["INF", "I", "ETH", "ER", "KR"];

    public static readonly ImmutableArray<RuleDefinition> Definitions =
    [
        new("CATALOG-LF-SEMESTERS", "Leistungsfach-Angebot umfasst HJ 1–4", "Pflichtenheft §6 Nr.1,12", "Alle LF-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-LF-EXCLUDED", "INF, I, ETH, ER, KR am GSM nicht als LF", "Pflichtenheft §4", "LF-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-PSY", "Psychologie: Wahlfach, HJ 1–2, zweistündig", "Pflichtenheft §4", "PSY-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-SIA", "SIA: Seminarkurs HJ 1–2, dreistündig", "Pflichtenheft §4", "SIA-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-GEO-BF", "Geographie-BF nur HJ 3–4", "Pflichtenheft §4", "GEO-BF-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-INF", "Informatik: BF HJ 1–4, dreistündig, mit Vorbildungsprüfung", "Pflichtenheft §4, §6 Nr.5", "INF-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-I-PROFILE", "Italienisch nur mit Profilprüfung", "Pflichtenheft §4, §6 Nr.6", "I-Angebote", ChoiceValidator.Version, ReviewStatus.Invalid),
        new("CATALOG-BILINGUAL", "Bilinguale Variante nur mit Bili-Vorbildungsprüfung", "Pflichtenheft §4, §6 Nr.7", "Bilinguale Angebote", ChoiceValidator.Version, ReviewStatus.Invalid)
    ];

    public static ImmutableArray<RuleMessage> Check(OfferCatalog catalog) => [.. catalog.Offers.SelectMany(offer => Check(offer))];

    public static ImmutableArray<RuleMessage> Check(CourseOffer offer)
    {
        var semesters = offer.Schedule.Select(s => s.Semester).Order().ToArray();
        var messages = ImmutableArray.CreateBuilder<RuleMessage>();
        void Require(bool condition, string id, string explanation)
        {
            if (!condition) messages.Add(new(Definitions.Single(d => d.Id == id), [offer.SubjectId], [.. semesters], $"Angebot {offer.Id}: {explanation}", Remedy));
        }
        Require(offer.Type != CourseType.Lf || semesters.SequenceEqual([1, 2, 3, 4]), "CATALOG-LF-SEMESTERS", "Leistungsfächer benötigen HJ 1–4.");
        Require(offer.Type != CourseType.Lf || !ExcludedLfSubjects.Contains(offer.SubjectId), "CATALOG-LF-EXCLUDED", "Dieses Fach wird am GSM nicht als Leistungsfach angeboten.");
        Require(offer.SubjectId != "PSY" || (offer.Type == CourseType.Elective && semesters.SequenceEqual([1, 2]) && offer.Schedule.All(s => s.WeeklyHours == 2)),
            "CATALOG-PSY", "Psychologie ist ein zweistündiges Wahlfach nur in HJ 1–2.");
        Require(offer.SubjectId != "SIA" || (offer.Type == CourseType.Seminar && semesters.SequenceEqual([1, 2]) && offer.Schedule.All(s => s.WeeklyHours == 3)),
            "CATALOG-SIA", "SIA ist ein dreistündiger Seminarkurs in HJ 1–2.");
        Require(offer.SubjectId != "GEO" || offer.Type == CourseType.Lf || (offer.Type == CourseType.Bf && semesters.SequenceEqual([3, 4])),
            "CATALOG-GEO-BF", "Geographie gibt es außerhalb des LF nur als BF in HJ 3–4 (keine Wahlkurse).");
        Require(offer.SubjectId != "INF" || (offer.Type == CourseType.Bf && semesters.SequenceEqual([1, 2, 3, 4])
            && offer.Schedule.All(s => s.WeeklyHours == 3) && offer.Requirements.Contains(Requirement.Informatics)),
            "CATALOG-INF", "Informatik benötigt BF HJ 1–4, drei Stunden und Vorbildungsprüfung.");
        Require(offer.SubjectId != "I" || ((offer.Type is CourseType.Bf or CourseType.Lf) && semesters.SequenceEqual([1, 2, 3, 4]) && offer.Requirements.Contains(Requirement.ItalianProfile)),
            "CATALOG-I-PROFILE", "Italienisch benötigt HJ 1–4 und die Profilprüfung.");
        Require(offer.Variant != CourseVariant.Bilingual || offer.Requirements.Contains(Requirement.ContinuousBilingual),
            "CATALOG-BILINGUAL", "Bilinguale Angebote benötigen die Bili-Vorbildungsprüfung.");
        return messages.ToImmutable();
    }
}
