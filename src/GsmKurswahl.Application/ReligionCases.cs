using System.Collections.Immutable;
using GsmKurswahl.Domain;
using GsmKurswahl.Rules;

namespace GsmKurswahl.Application;

// One pupil whose religion/ethics choice differs from class 10. AssessmentSubject is set when that subject is
// planned for the oral exam and the teacher stock has not decided the exam permission yet
// (AGVO §21 Abs.2 Nr.4/5: assessment at the start of HJ 1). Planning aid only, never an admission.
public sealed record ReligionCase(TeacherState State, string Grade10, ImmutableArray<string> Chosen,
    string? AssessmentSubject, bool ChangeDuringCourse)
{
    public string Note => AssessmentSubject is not null ? $"Feststellungsprüfung {AssessmentSubject} (mündlich geplant)"
        : ChangeDuringCourse ? $"Wechsel in der Kursstufe: {string.Join(" → ", Chosen)}"
        : $"Wechsel {Grade10} → {string.Join("/", Chosen)}";
}

public static class ReligionCases
{
    public static ReligionCase? For(TeacherState state)
    {
        if (state.Active is null || state.Record.Student.Prerequisites.ReligionEthicsGrade10 is not { } grade10) return null;
        var profile = new ProfileContext(ProfileStatus.Verified, state.Record.Student.Prerequisites);
        var choice = state.Active.Snapshot;
        var changed = AcademicRules.ReligionChange(choice, profile);
        var assessment = AcademicRules.AssessmentSubject(choice, profile);
        if (changed.Length == 0 && assessment is null) return null;
        var all = choice.Selections.Where(s => s.SubjectId is "ETH" or "ER" or "KR" && !s.Semesters.IsEmpty)
            .OrderBy(s => s.Semesters.Min()).Select(s => s.SubjectId).Distinct().ToImmutableArray();
        return new(state, grade10, all.Length > 1 ? all : [.. changed], assessment, all.Length > 1);
    }

    public static ImmutableArray<ReligionCase> All(IEnumerable<TeacherState> states) =>
        [.. states.Select(For).Where(c => c is not null).Select(c => c!)];
}
