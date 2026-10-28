namespace GsmKurswahl.Application.Tests;

public class PlanningDisplayTests
{
    [Fact]
    public void CompressesOnlyIdenticalPeriodsAndKeepsIrregularSemestersVisible()
    {
        Assert.Equal("24", PlanningDisplay.PeriodCounts([24, 24, 24, 24]));
        Assert.Equal("0", PlanningDisplay.PeriodCounts([0, 0, 0, 0]));
        Assert.Equal("HJ 1: 24 · HJ 2: 24 · HJ 3: 24 · HJ 4: 24", PlanningDisplay.PeriodCounts([24, 24, 24, 24], true));
        Assert.Equal("HJ 3: 20 · HJ 4: 12", PlanningDisplay.PeriodCounts([null, null, 20, 12], true));
        Assert.Equal("12 KS1", PlanningDisplay.PeriodCounts([12, 12, null, null]));
        Assert.Equal("18 KS2", PlanningDisplay.PeriodCounts([null, null, 18, 18]));
        Assert.Equal("20 / 18 KS1/2", PlanningDisplay.PeriodCounts([20, 20, 18, 18]));
        Assert.Equal("1:20 · 2:12 · 3:18 · 4:18", PlanningDisplay.PeriodCounts([20, 12, 18, 18]));
        Assert.Equal("1:– · 2:– · 3:20 · 4:12", PlanningDisplay.PeriodCounts([null, null, 20, 12]));
    }
}
