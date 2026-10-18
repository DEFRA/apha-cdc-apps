using CDC.Common.Contracts;
using CDC.Web.Features.DiseaseRanking;

namespace CDC.Web.Tests.Features.DiseaseRanking;

public class ChartLayoutTests
{
    private static DiseaseRankingChartDto Chart(double min, double max, double interval, int bars) => new()
    {
        AxisMin = min,
        AxisMax = max,
        AxisInterval = interval,
        Bars = [.. Enumerable.Range(0, bars).Select(_ => new DiseaseRankingBarDto())]
    };

    [Fact]
    public void HasBars_IsFalse_WhenThereAreNoBarsOrNoAxisRange()
    {
        Assert.False(new ChartLayout(Chart(0, 500, 50, 0)).HasBars);
        Assert.False(new ChartLayout(Chart(0, 0, 50, 3)).HasBars);
        Assert.True(new ChartLayout(Chart(0, 500, 50, 3)).HasBars);
    }

    [Fact]
    public void BodyHeight_IsTheRowHeightTimesTheNumberOfBars()
    {
        Assert.Equal(13 * 4, new ChartLayout(Chart(0, 500, 50, 4)).BodyHeight);
    }

    [Fact]
    public void RowTop_StepsByTheRowHeight() => Assert.Equal(26, ChartLayout.RowTop(2));

    [Theory]
    [InlineData(-100, 360)]
    [InlineData(500, 715)]
    [InlineData(200, 537.5)]
    public void X_MapsTheAxisRangeOntoThePlotArea(double value, double expected)
    {
        var layout = new ChartLayout(Chart(-100, 500, 50, 1));

        Assert.Equal(expected, layout.X(value), precision: 6);
    }

    [Fact]
    public void Ticks_StartAtTheFirstMultipleOfTheIntervalAndIncludeTheMaximum()
    {
        var ticks = new ChartLayout(Chart(-100, 100, 50, 1)).Ticks.ToList();

        Assert.Equal([-100d, -50d, 0d, 50d, 100d], ticks);
    }

    [Fact]
    public void Ticks_RoundTheStartUpToTheNextInterval()
    {
        var ticks = new ChartLayout(Chart(-75, 60, 50, 1)).Ticks.ToList();

        Assert.Equal([-50d, 0d, 50d], ticks);
    }

    [Fact]
    public void Ticks_AreEmpty_WhenTheIntervalIsNotPositive()
    {
        Assert.Empty(new ChartLayout(Chart(0, 500, 0, 1)).Ticks);
    }
}
