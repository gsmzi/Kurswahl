using System.Collections.Immutable;
using GsmKurswahl.Domain;

namespace GsmKurswahl.Tests;

public static class TestData
{
    public static OfferCatalog WorkflowCatalog
    {
        get
        {
            string[] ids = ["D", "M", "E", "BIO", "CH", "G", "GK", "GEO", "W", "SP", "BK", "MU", "ETH"];
            var offers = ids.Select(s => Offer(s + "-BF", s)).ToList();
            offers.RemoveAll(o => o.SubjectId is "GEO" or "W");
            offers.Add(Offer("GEO-BF", "GEO") with { Schedule = [new(3, 2), new(4, 2)] });
            offers.AddRange(new[] { "D", "M", "E", "BIO", "CH", "W" }.Select(s => Offer(s + "-LF", s, CourseType.Lf)));
            return Catalog with { Subjects = [.. ids.Select(s => new Subject(s, s))], Offers = [.. offers] };
        }
    }
    public static CourseOffer Offer(string id = "D-BF", string subject = "D", CourseType type = CourseType.Bf) =>
        new(id, subject, type, CourseVariant.Normal, OfferStatus.Offered,
            [new(1, 3), new(2, 3), new(3, 3), new(4, 3)], []);
    public static OfferCatalog Catalog => new(1, "demo-2029-v1", 2029, true, [new("D", "Deutsch")], [Offer()]);
    public static CourseChoice Choice => new(2, Guid.NewGuid(), "demo-001", 2029, "demo-2029-v1", "m1-2",
        1, DateTimeOffset.Parse("2026-10-25T12:00:00Z"), DateTimeOffset.Parse("2026-10-25T12:00:00Z"),
        SubmissionStatus.Draft, ElectionPhase.Preliminary, [], []);
    public static CourseSelection Select(CourseOffer offer) => new(offer.Id, offer.SubjectId, offer.Type,
        offer.Variant, offer.Schedule.Select(s => s.Semester).ToImmutableArray());
}
