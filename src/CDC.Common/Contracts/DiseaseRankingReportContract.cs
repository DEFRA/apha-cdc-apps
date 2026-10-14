namespace CDC.Common.Contracts;

/// <summary>Which of the two disease ranking reports to build.</summary>
public enum DiseaseRankingReportKind
{
    /// <summary>The "Scored RFI graphs" report (legacy <c>ProfileRankingReportPdf</c>).</summary>
    Scored = 0,

    /// <summary>The "Ranked RFI graphs" report (legacy <c>ProfileRankingReportRFIPdf</c>).</summary>
    Ranked = 1
}

/// <summary>An entry in the "select a report" dropdown, from <c>GET /api/disease-ranking/report-options</c>.</summary>
public sealed record DiseaseRankingReportOptionDto
{
    /// <summary>Gets the key to request the report with: <c>terrestrial</c>, <c>aquatic</c> or a filter id.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the display text.</summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>One row of a "top 25" summary table.</summary>
public sealed record DiseaseRankingTopRowDto
{
    /// <summary>Gets the disease and species label.</summary>
    public string Disease { get; init; } = string.Empty;

    /// <summary>Gets the rank position; suffixed with <c>=</c> when tied.</summary>
    public string Rank { get; init; } = string.Empty;

    /// <summary>Gets the score for the reason for intervention.</summary>
    public int Score { get; init; }

    /// <summary>Gets the post mitigation risk score.</summary>
    public int Pmr { get; init; }

    /// <summary>Gets a value indicating whether the disease is exotic.</summary>
    public bool IsExotic { get; init; }
}

/// <summary>The "top 25" summary table for one reason for intervention.</summary>
public sealed record DiseaseRankingTopTableDto
{
    /// <summary>Gets the reason for intervention, in capitals.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the header colour as a <c>#RRGGBB</c> value.</summary>
    public string Colour { get; init; } = string.Empty;

    /// <summary>Gets the rows, highest ranked first.</summary>
    public IReadOnlyList<DiseaseRankingTopRowDto> Rows { get; init; } = [];
}

/// <summary>A legend entry for a chart series.</summary>
public sealed record DiseaseRankingSeriesDto
{
    /// <summary>Gets the series name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the bar colour as a <c>#RRGGBB</c> value.</summary>
    public string Colour { get; init; } = string.Empty;
}

/// <summary>One stacked segment of a bar, positioned on the value axis.</summary>
public sealed record DiseaseRankingSegmentDto
{
    /// <summary>Gets the series the segment belongs to.</summary>
    public string Series { get; init; } = string.Empty;

    /// <summary>Gets the segment colour as a <c>#RRGGBB</c> value.</summary>
    public string Colour { get; init; } = string.Empty;

    /// <summary>Gets the lower axis value; negative for bars drawn left of the axis origin.</summary>
    public double Start { get; init; }

    /// <summary>Gets the upper axis value.</summary>
    public double End { get; init; }
}

/// <summary>One disease bar in a chart.</summary>
public sealed record DiseaseRankingBarDto
{
    /// <summary>Gets the label, truncated to the legacy 50 characters.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the stacked segments.</summary>
    public IReadOnlyList<DiseaseRankingSegmentDto> Segments { get; init; } = [];
}

/// <summary>One bar chart.</summary>
public sealed record DiseaseRankingChartDto
{
    /// <summary>Gets the chart title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the explanatory paragraphs shown above the chart.</summary>
    public IReadOnlyList<string> Paragraphs { get; init; } = [];

    /// <summary>Gets the axis minimum.</summary>
    public double AxisMin { get; init; }

    /// <summary>Gets the axis maximum.</summary>
    public double AxisMax { get; init; }

    /// <summary>Gets the distance between axis gridlines.</summary>
    public double AxisInterval { get; init; }

    /// <summary>Gets a value indicating whether the chart starts on a new page.</summary>
    public bool StartsOnNewPage { get; init; }

    /// <summary>Gets the legend entries.</summary>
    public IReadOnlyList<DiseaseRankingSeriesDto> Legend { get; init; } = [];

    /// <summary>Gets the bars, highest impact first (top of the chart).</summary>
    public IReadOnlyList<DiseaseRankingBarDto> Bars { get; init; } = [];
}

/// <summary>
/// A fully computed disease ranking report, as returned by
/// <c>GET /api/disease-ranking/reports/{reportKey}</c>.
/// </summary>
public sealed record DiseaseRankingReportDto
{
    /// <summary>Gets the report kind.</summary>
    public DiseaseRankingReportKind Kind { get; init; }

    /// <summary>Gets the front page heading.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the front page introduction.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the title shown in each page footer.</summary>
    public string FooterTitle { get; init; } = string.Empty;

    /// <summary>Gets a name suitable for the downloaded file, without extension.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Gets the summary tables, one per reason for intervention.</summary>
    public IReadOnlyList<DiseaseRankingTopTableDto> TopTables { get; init; } = [];

    /// <summary>Gets the charts in page order.</summary>
    public IReadOnlyList<DiseaseRankingChartDto> Charts { get; init; } = [];
}
