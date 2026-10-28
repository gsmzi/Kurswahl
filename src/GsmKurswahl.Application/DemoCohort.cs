using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public sealed record DemoStudent(StudentRecord Record, CourseChoice Choice);

/// <summary>Explicit synthetic fixture, validated by the production rules; never overrides release gates.</summary>
public static class DemoCohort
{
    public static ImmutableArray<DemoStudent> Create(OfferCatalog catalog)
    {
        if (!catalog.IsDemo) throw new ArgumentException("Testbestand benötigt einen Demo-Katalog.");
        string[][] triples = [
            ["D-LF", "M-LF", "E-LF"], ["D-LF", "M-LF", "BIO-LF"],
            ["E-LF", "BIO-LF", "W-LF"], ["M-LF", "PH-LF", "G-LF"],
            ["D-LF", "F-LF", "BK-LF"], ["M-LF", "CH-LF", "E-LF"],
            ["E-LF", "BIO-LF-BILI", "G-LF"], ["D-LF", "L-LF", "MU-LF"],
            ["M-LF", "BIO-LF", "GK-LF"], ["D-LF", "E-LF", "W-LF"],
            ["M-LF", "PH-LF", "SP-LF"], ["E-LF", "F-LF", "GEO-LF"],
            ["D-LF", "CH-LF", "BIO-LF"], ["M-LF", "E-LF", "BK-LF"],
            ["D-LF", "BIO-LF-BILI", "G-LF"], ["M-LF", "L-LF", "W-LF"] ];
        var result = ImmutableArray.CreateBuilder<DemoStudent>();
        var time = DateTimeOffset.UtcNow;
        for (var index = 0; index < 80; index++)
        {
            var number = index + 1; var id = $"demo-{number:000}";
            var offers = triples[index % triples.Length].Select(id => catalog.Offers.Single(o => o.Id == id)).ToList();
            void Add(string offerId)
            {
                var offer = catalog.Offers.Single(o => o.Id == offerId);
                if (!offers.Any(o => o.SubjectId == offer.SubjectId)) offers.Add(offer);
            }
            var religion = new[] { "ETH", "ER", "KR" }[index % 3];
            var art = offers.Any(o => o.SubjectId == "BK") ? "BK" : offers.Any(o => o.SubjectId == "MU") ? "MU" : index % 2 == 0 ? "BK" : "MU";
            foreach (var subject in new[] { "D", "M", "E", "G", "SP", "GK", "GEO", art, religion }) Add(subject + "-BF");
            Add(index % 4 == 0 ? "BIO-BF-BILI" : "BIO-BF");
            Add(index % 5 == 0 ? "INF-BF" : index % 5 == 1 ? "F-BF" : index % 5 == 2 ? "L-BF" : "CH-BF");
            if (index % 3 == 0) Add("PSY-WF");
            if (index % 4 == 0) Add("SIA-SK");
            var prerequisites = new PriorEducation(Knowledge.No, index % 2 == 0 ? Knowledge.Yes : Knowledge.No,
                index % 2 == 0 ? Knowledge.No : Knowledge.Yes, offers.Any(o => o.Variant == CourseVariant.Bilingual) ? Knowledge.Yes : Knowledge.No,
                religion, []) { ContinuedLanguages = ["E", "F", "L"], ReligionEthicsPermission = Knowledge.Yes,
                ReligionEthicsExamPermission = Knowledge.Yes, PermanentSportExemption = Knowledge.No };
            var profile = new ProfileContext(ProfileStatus.Verified, prerequisites);
            CourseSelection Selection(CourseOffer o)
            {
                var semesters = o.Schedule.Select(s => s.Semester).ToArray();
                var economics = offers.Any(x => x.Id == "W-LF");
                if (o.Type == CourseType.Bf && o.SubjectId == "GK")
                    semesters = economics ? (index % 3) switch { 0 => new[] { 1 }, 1 => new[] { 1, 3, 4 }, _ => new[] { 1, 2, 3, 4 } }
                        : index % 2 == 0 ? [1, 2] : [1, 2, 3, 4];
                if (o.Type == CourseType.Bf && o.SubjectId == "GEO" && economics && index % 2 == 0) semesters = [3];
                return new(o.Id, o.SubjectId, o.Type, o.Variant, [.. semesters]);
            }
            var choice = new CourseChoice(2, Guid.NewGuid(), id, catalog.GraduationYear, catalog.Version, ChoiceValidator.Version,
                1, time, time, SubmissionStatus.Draft, ElectionPhase.Preliminary,
                [.. offers.Select(Selection)], []);
            // A breadth offer may already be an LF. Fill the minimum with an additional offered language/science if necessary.
            foreach (var extra in new[] { "CH-BF", "PH-BF", "F-BF", "L-BF" })
            {
                if (choice.Selections.Sum(s => s.Semesters.Length) >= 42) break;
                Add(extra);
                choice = choice with { Selections = [.. offers.Select(Selection)] };
            }
            var exams = AcademicRules.ExamOptions(choice, catalog, profile);
            if (exams.IsEmpty) throw new InvalidOperationException($"{id}: keine Prüfungskombination im Demoentwurf.");
            var exam = exams[index % exams.Length]; choice = choice with { PlannedOralSubjects = [exam.First, exam.Second] };
            var validation = new ChoiceValidator().Validate(choice, catalog, profile);
            var errors = validation.Messages.Where(m => m.Rule.Severity == ReviewStatus.Invalid).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(id + ": " + string.Join("; ", errors.Select(m => m.Rule.Id + " " + m.Explanation)));
            var departure = index % 20 == 19 ? DepartureStatus.ConfirmedDeparture : index % 20 >= 17 ? DepartureStatus.ExpectedDeparture : DepartureStatus.Active;
            var record = new StudentRecord(1, new(Guid.NewGuid(), id, $"Testperson {number:000}, Demo", "10" + (char)('a' + index / 20),
                catalog.GraduationYear, departure, prerequisites), "", "", "", false, 1, "demo-generator", time, null);
            result.Add(new(record, choice));
        }
        return result.ToImmutable();
    }
}
