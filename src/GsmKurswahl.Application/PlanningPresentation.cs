using GsmKurswahl.Domain;

namespace GsmKurswahl.Application;

public static class PlanningPresentation
{
    // School planning preference, not an additional legal eligibility rule.
    public static bool CanCancel(CourseOffer offer) => offer.SubjectId is not ("D" or "M" or "E")
        && !(offer.Type == CourseType.Bf && offer.SubjectId is "G" or "GK" or "GEO" or "SP" or "ETH" or "ER" or "KR" or "BK" or "MU");

    public static string Choices(CourseChoice choice, bool lf) => string.Join(" · ", choice.Selections
        .Where(s => (s.Type == CourseType.Lf) == lf).Select(s => s.SubjectId
            + (s.Variant == CourseVariant.Bilingual ? " bili" : "")
            + (s.Semesters.SequenceEqual(new[] { 1, 2, 3, 4 }) && s.SubjectId is not ("GK" or "GEO") ? "" : " (HJ " + string.Join(",", s.Semesters) + ")")));

    public static string[][] DemandTable(PlanningDocument document)
    {
        var analysis = CoursePlanning.Analyze(document);
        return document.Catalog.Subjects.OrderBy(s => s.Name, StringComparer.CurrentCulture).Select(subject =>
        {
            string Cell(CourseType type, CourseVariant variant, bool extra = false)
            {
                var offers = document.Catalog.Offers.Where(o => o.SubjectId == subject.Id && o.Variant == variant
                    && (extra ? o.Type is CourseType.Elective or CourseType.Seminar : o.Type == type));
                var values = offers.Select(o => o.Status is OfferStatus.Offered or OfferStatus.Established
                    ? PlanningDisplay.PeriodCounts(Enumerable.Range(1, 4).Select(h => o.Schedule.Any(s => s.Semester == h)
                        ? (int?)analysis.Demand.Single(d => d.OfferId == o.Id && d.Semester == h).Relevant : null).ToArray(), type == CourseType.Bf && subject.Id is "GK" or "GEO")
                    : o.Status == OfferStatus.Cancelled ? "entfällt" : "geplant").ToArray();
                return values.Length == 0 ? "-" : string.Join(" / ", values);
            }
            return new[] { subject.Id + " - " + subject.Name, Cell(CourseType.Lf, CourseVariant.Normal), Cell(CourseType.Lf, CourseVariant.Bilingual),
                Cell(CourseType.Bf, CourseVariant.Normal), Cell(CourseType.Bf, CourseVariant.Bilingual), Cell(CourseType.Elective, CourseVariant.Normal, true) };
        }).ToArray();
    }
}
