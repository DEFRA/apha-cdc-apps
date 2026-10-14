using CDC.Common.Contracts;

namespace CDC.Web.Features.DiseaseRanking;

/// <summary>
/// Pixel geometry for drawing one <see cref="DiseaseRankingChartDto"/> as an HTML bar chart: labels
/// in the left half, bars on a value axis in the right half, as the legacy chart image laid out.
/// </summary>
/// <param name="chart">The chart to lay out.</param>
public sealed class ChartLayout(DiseaseRankingChartDto chart)
{
    /// <summary>Total drawing width.</summary>
    public const double Width = 720;

    /// <summary>Vertical space given to each bar.</summary>
    public const double RowHeight = 13;

    /// <summary>Thickness of each bar.</summary>
    public const double BarHeight = 8;

    /// <summary>Left edge of the plot area; labels end just before it.</summary>
    public const double PlotLeft = 360;

    /// <summary>Width of the plot area.</summary>
    public const double PlotWidth = 355;

    /// <summary>Gets a value indicating whether there is anything to draw.</summary>
    public bool HasBars => chart.Bars.Count > 0 && chart.AxisMax > chart.AxisMin;

    /// <summary>Gets the height of the area holding the bars.</summary>
    public double BodyHeight => chart.Bars.Count * RowHeight;

    /// <summary>Gets the y coordinate of the top of the row at <paramref name="index"/>.</summary>
    /// <param name="index">The zero-based row, top first.</param>
    public static double RowTop(int index) => index * RowHeight;

    /// <summary>Converts an axis value to an x coordinate.</summary>
    /// <param name="value">The axis value.</param>
    public double X(double value) =>
        PlotLeft + ((value - chart.AxisMin) / (chart.AxisMax - chart.AxisMin) * PlotWidth);

    /// <summary>Gets the axis values to draw gridlines and labels at.</summary>
    public IEnumerable<double> Ticks
    {
        get
        {
            if (chart.AxisInterval <= 0)
            {
                yield break;
            }

            for (var tick = Math.Ceiling(chart.AxisMin / chart.AxisInterval) * chart.AxisInterval;
                 tick <= chart.AxisMax;
                 tick += chart.AxisInterval)
            {
                yield return tick;
            }
        }
    }
}
