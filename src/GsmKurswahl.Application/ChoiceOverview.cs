using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

public enum ChoiceStateFilter { All, WithoutChoice, Incomplete, Violation, NeedsReview, Valid, ReselectionOpen, AssessmentNeeded }

// Filter of the teacher's choice overview. OfferId narrows to pupils attending that offer (optionally in one semester).
public sealed record ChoiceFilter(string Text = "", string? ClassName = null, ChoiceStateFilter State = ChoiceStateFilter.All,
    string? OfferId = null, int? Semester = null);

public sealed record CourseMember(StudentRecord Student, ImmutableArray<int> Semesters);
public sealed record CourseList(CourseOffer Offer, string Title, ImmutableArray<CourseMember> Members);

/// <summary>Pure selection logic for the teacher's choice page and its printouts; no rule of its own.</summary>
public static class ChoiceOverview
{
    // Rules whose violation means that courses are still missing (as opposed to a wrong or unclear entry).
    private static readonly string[] MissingRuleIds = ["AGVO-LF", "AGVO-COURSES", "AGVO-REQUIRED", "AGVO-LANGUAGE-SCIENCE", "AGVO-SPORT-REPLACEMENT"];

    public static bool IsIncomplete(TeacherState state) => state.Active is null
        || state.Active.Snapshot.PlannedOralSubjects.Length != 2
        || (state.Validation?.Messages.Any(m => m.Rule.Severity == ReviewStatus.Invalid && MissingRuleIds.Contains(m.Rule.Id)) ?? true);

    public static ImmutableArray<string> Violations(ValidationResult? validation) =>
        [.. (validation?.Messages ?? []).Where(m => m.Rule.Severity == ReviewStatus.Invalid).Select(m => m.Explanation).Distinct()];
    public static ImmutableArray<string> Hints(ValidationResult? validation) =>
        [.. (validation?.Messages ?? []).Where(m => m.Rule.Severity != ReviewStatus.Invalid).Select(m => m.Explanation).Distinct()];

    public static ImmutableArray<string> Classes(IEnumerable<TeacherState> states) =>
        [.. states.Select(s => s.Record.Student.ClassName).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order(StringComparer.CurrentCulture)];

    public static ImmutableArray<TeacherState> Apply(IEnumerable<TeacherState> states, ChoiceFilter filter) => [.. states.Where(state =>
    {
        var student = state.Record.Student;
        if (!string.IsNullOrWhiteSpace(filter.Text)
            && !(student.SyntheticDisplayName + " " + student.ClassName + " " + student.ExternalStudentId).Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (filter.ClassName is not null && student.ClassName != filter.ClassName) return false;
        if (filter.OfferId is not null && !(state.Active?.Snapshot.Selections.Any(s => s.OfferId == filter.OfferId
            && (filter.Semester is not { } h || s.Semesters.Contains(h))) ?? false)) return false;
        if (filter.OfferId is null && filter.Semester is { } semester && state.Active is null) return false;
        return filter.State switch
        {
            ChoiceStateFilter.WithoutChoice => state.Active is null,
            ChoiceStateFilter.Incomplete => IsIncomplete(state),
            ChoiceStateFilter.Violation => state.Validation?.Status == ReviewStatus.Invalid,
            ChoiceStateFilter.NeedsReview => state.Validation?.Status == ReviewStatus.NeedsReview,
            ChoiceStateFilter.Valid => state.Validation?.Status == ReviewStatus.Valid,
            ChoiceStateFilter.ReselectionOpen => state.RequiresReselection,
            ChoiceStateFilter.AssessmentNeeded => ReligionCases.For(state)?.AssessmentSubject is not null,
            _ => true
        };
    })];

    public static string OfferTitle(OfferCatalog catalog, CourseOffer offer) =>
        (catalog.Subjects.FirstOrDefault(s => s.Id == offer.SubjectId)?.Name ?? offer.SubjectId) + " "
        + offer.Type switch { CourseType.Lf => "LF", CourseType.Bf => "BF", CourseType.Elective => "Wahlfach", _ => "Seminarkurs" }
        + (offer.Variant == CourseVariant.Bilingual ? " bilingual" : "") + CourseAnnotations.Suffix(offer);

    // Everyone attending the offer (in the given semester). Confirmed leavers are listed too; the caller decides about display.
    public static CourseList Members(IEnumerable<TeacherState> states, OfferCatalog catalog, string offerId, int? semester = null)
    {
        var offer = catalog.Offers.Single(o => o.Id == offerId);
        var members = states.Select(s => (State: s, Selection: s.Active?.Snapshot.Selections.FirstOrDefault(c => c.OfferId == offerId)))
            .Where(x => x.Selection is not null && (semester is not { } h || x.Selection.Semesters.Contains(h)))
            .OrderBy(x => x.State.Record.Student.ClassName, StringComparer.CurrentCulture)
            .ThenBy(x => x.State.Record.Student.SortName().Last, StringComparer.CurrentCulture)
            .ThenBy(x => x.State.Record.Student.SortName().First, StringComparer.CurrentCulture)
            .Select(x => new CourseMember(x.State.Record, [.. x.Selection!.Semesters.Order()])).ToImmutableArray();
        return new(offer, OfferTitle(catalog, offer) + (semester is { } hj ? " · HJ " + hj : ""), members);
    }

    public static ImmutableArray<CourseList> AllCourseLists(IEnumerable<TeacherState> states, OfferCatalog catalog)
    {
        var all = states.ToArray();
        return [.. catalog.Offers.OrderBy(o => catalog.Subjects.FirstOrDefault(s => s.Id == o.SubjectId)?.Name ?? o.SubjectId, StringComparer.CurrentCulture)
            .ThenBy(o => o.Type).ThenBy(o => o.Variant).Select(o => Members(all, catalog, o.Id)).Where(l => !l.Members.IsEmpty)];
    }

    // The teacher's working copy is judged exactly like every stored choice: current offer, current rules, protected prerequisites.
    public static ValidationResult Validate(CourseChoice choice, OfferCatalog catalog, StudentRecord record) => new ChoiceValidator().Validate(
        choice with { OfferVersion = catalog.Version, RuleVersion = ChoiceValidator.Version }, catalog,
        new ProfileContext(ProfileStatus.Verified, record.Student.Prerequisites));
}
