namespace GsmKurswahl.Application;

public static class PlanningDisplay
{
    // Null denotes an unscheduled semester; zero is a scheduled offer with no demand.
    public static string PeriodCounts(IReadOnlyList<int?> counts, bool explicitSemesters = false)
    {
        if (counts.Count != 4) throw new ArgumentException("Vier Halbjahre erforderlich.");
        string N(int? value) => value?.ToString() ?? "–";
        if (explicitSemesters) return string.Join(" · ", counts.Select((c, i) => (c, i)).Where(x => x.c is not null).Select(x => $"HJ {x.i + 1}: {N(x.c)}"));
        if (counts.Distinct().Count() == 1) return N(counts[0]);
        if (counts[0] == counts[1] && counts[2] == counts[3])
        {
            if (counts[2] is null) return N(counts[0]) + " KS1";
            if (counts[0] is null) return N(counts[2]) + " KS2";
            return N(counts[0]) + " / " + N(counts[2]) + " KS1/2";
        }
        return string.Join(" · ", counts.Select((c, i) => $"{i + 1}:{N(c)}"));
    }
}
