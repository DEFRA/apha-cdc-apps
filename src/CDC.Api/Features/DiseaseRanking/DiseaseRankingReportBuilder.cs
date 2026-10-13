using System.Globalization;
using CDC.Common.Contracts;

namespace CDC.Api.Features.DiseaseRanking;

/// <summary>Names used in the report headings, footer and file name.</summary>
/// <param name="HeadingName">The name in the front page heading, e.g. <c>Terrestrial Diseases</c>.</param>
/// <param name="FooterName">The name in the page footer, e.g. <c>Terrestrial</c>.</param>
internal sealed record DiseaseRankingScope(string HeadingName, string FooterName);

/// <summary>
/// Builds the two disease ranking reports, porting the legacy <c>ProfileRankingReportPdf</c> (scored
/// graphs) and <c>ProfileRankingReportRFIPdf</c> (ranked graphs) data selection and chart layout.
/// </summary>
internal static class DiseaseRankingReportBuilder
{
    private const int TopTableRows = 25;
    private const int RfiChartRows = 50;
    private const int TotalChartRows = 60;
    private const double AxisInterval = 50;
    private const double RankedAxisMax = 500;
    private const string RiskColour = "#000000";

    private const string ChartKeyText =
        "Note: *=Disease profiles awaiting validation.";

    private const string ScoredIntroText =
        "Top 50 Graphical Disease Reports, showing the top scoring profiles, with a separate graph for EXOTIC and endemic diseases, " +
        "for each RFI. Each graph is ranked by the RFI, then by the post mitigation risk, (Black) which has been multiplied by -1 to " +
        "separate it from the RFI bars in each graph. " + ChartKeyText;

    private const string RankedIntroText =
        "Graphical report showing at most the top 50 ranked profiles for each RFI. Each disease will only have (at most) one entry " +
        "regardless of how many species have been considered in the profile. For multiple species disease profiles the species that " +
        "will appear in this graph is the one with the largest sum of the PMR score and RFI importance. Each graph is designed to show " +
        "the post mitigation risk score (PMR, coloured black) next to the axis and with the ranking for the specific RFI shown next to it. " +
        "Overall potential disease impact for that RFI is the combination of the PMR score and the RFI ranking, with the highest impact at " +
        "the top of the graph. Diseases that do not achieve a ranking for this RFI will not appear in this graph. Note: * = Disease " +
        "profiles awaiting validation. EXOTIC DISEASES APPEAR IN CAPITALS and endemic diseases in standard case format.";

    private const string TradeText =
        "The Animal Health and Welfare Strategy sets out four reasons for Government intervention. These include International Trade. " +
        "The presence of animal disease, either at the national, regional or local (on farm) level, can reduce our ability to trade. " +
        "The EU and OIE operate trade rule systems that help to reduce the risk of animals or animal products spreading disease. " +
        "In order to trade at a national level without restrictions, countries must maintain disease free status for notifiable diseases. " +
        "The D2R2 scores for International Trade reflect trade from the UK at a national or regional level, not at a farm level. " +
        "Most of the endemic diseases in D2R2 are not notifiable or if they are do not have an impact on national trading patterns. " +
        "Therefore the graph of endemic diseases does not contain many diseases.";

    private const string RankedTradeText = TradeText + " Therefore the international trade graph contains very few (if any) endemic diseases.";

    private const string TotalText =
        "Graphical Disease Report, showing all the scored profiles ranked after addition of the score for each individual RFI, but with " +
        "post mitigation risk (Black) score multiplied by -1 to separate it from the RFI bars in each graph. This graph allows the score " +
        "for every profile to be identified even if it is not near the top of the rankings for any of the RFIs. However this total score " +
        "should not be used for resource allocation decisions - the rankings per RFI are the more appropriate tool to use to support " +
        "decision making. Note: *=Disease profiles awaiting validation; EXOTIC diseases appear in capitals and endemic diseases in standard case format.";

    private const string TotalIncludingRiskText =
        "Graphical Disease Report, showing all the scored profiles ranked after addition of the score for each individual RFI and also " +
        "including the post mitigation risk (Black) score. This graph allows the score for every profile to be identified even if it is " +
        "not near the top of the rankings for any of the RFIs. However this total score should not be used for resource allocation " +
        "decisions- the rankings per RFI are the more appropriate tool to use to support decision making. Note: *=Disease profiles " +
        "awaiting validation; EXOTIC diseases appear in capitals and endemic diseases in standard case format.";

    private const string TotalTitle = "Diseases ranked by total relative impact on the four reasons for government intervention";

    private const string TotalIncludingRiskTitle =
        "Diseases ranked by post mitigation risk plus total relative impact on the four reasons for government intervention";

    private const string Subtitle =
        "This report presents a table of rankings for each reason for intervention (RFI) for diseases both exotic (giving anticipated " +
        "potential impact should disease reach GB) and endemic to the UK with graphical representations on the following pages.";

    /// <summary>Builds a report from the profiles selected for it.</summary>
    /// <param name="kind">Scored or ranked graphs.</param>
    /// <param name="scope">The names to use in headings.</param>
    /// <param name="profiles">The ranked profiles the report covers.</param>
    public static DiseaseRankingReportDto Build(
        DiseaseRankingReportKind kind,
        DiseaseRankingScope scope,
        IReadOnlyList<Domain.Entities.RankedProfile> profiles)
    {
        var items = profiles.Select(profile => new RankItem(profile)).ToList();
        var isScored = kind == DiseaseRankingReportKind.Scored;

        return new DiseaseRankingReportDto
        {
            Kind = kind,
            Title = $"Issues ranked by impact on each of the four reasons for government intervention for {scope.HeadingName}",
            Subtitle = Subtitle,
            FooterTitle = isScored
                ? $"RFI scoring report for {scope.FooterName}."
                : $"Disease ranking report for {scope.FooterName}.",
            FileName = BuildFileName(kind, scope.FooterName),
            TopTables = [.. Rfi.All.Select(rfi => BuildTopTable(items, rfi, isScored))],
            Charts = isScored ? BuildScoredCharts(items) : BuildRankedCharts(items)
        };
    }

    private static string BuildFileName(DiseaseRankingReportKind kind, string name)
    {
        var safeName = string.Concat(name.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'));

        return $"DiseaseRankingReport_{kind}_{safeName.Trim()}";
    }

    private static DiseaseRankingTopTableDto BuildTopTable(IReadOnlyList<RankItem> items, Rfi rfi, bool isScored)
    {
        var ranked = RankListOperations.WithImpact(
            RankListOperations.GetFilteredSortedList(items, null, rfi.Category, onePerDisease: true),
            rfi.Category);

        RankListOperations.CalculateRankPosition(ranked, rfi.Category);

        var rows = new List<DiseaseRankingTopRowDto>();

        for (var index = ranked.Count - 1; index >= 0 && rows.Count < TopTableRows; index--)
        {
            var item = ranked[index];
            var isTied = (index < ranked.Count - 1 && ranked[index + 1].RankPosition == item.RankPosition)
                || (index > 0 && ranked[index - 1].RankPosition == item.RankPosition);

            rows.Add(new DiseaseRankingTopRowDto
            {
                Disease = item.FullTitleMixedCase(55),
                Rank = item.RankPosition.ToString(CultureInfo.InvariantCulture) + (isTied ? "=" : string.Empty),
                Score = item.GetRank(rfi.Category),
                Pmr = item.PositiveRiskRank,
                IsExotic = item.IsExotic
            });
        }

        return new DiseaseRankingTopTableDto
        {
            Name = ImpactName(rfi.Category).ToUpperInvariant(),
            Colour = Colour(rfi.Category, isScored),
            Rows = rows
        };
    }

    private static List<DiseaseRankingChartDto> BuildScoredCharts(IReadOnlyList<RankItem> items)
    {
        var charts = new List<DiseaseRankingChartDto>();

        foreach (var rfi in Rfi.All)
        {
            foreach (var isExotic in new[] { true, false })
            {
                var list = RankListOperations.WithImpact(
                    RankListOperations.GetFilteredSortedList(items, isExotic, rfi.Category, onePerDisease: true),
                    rfi.Category);

                var paragraphs = new List<string> { ScoredIntroText };

                if (!isExotic && rfi.Category == RankCategory.InternationalTrade)
                {
                    paragraphs.Add(TradeText);
                }

                var title = isExotic
                    ? $"Exotic diseases ranked according to potential impact on {ImpactName(rfi.Category)}"
                    : $"Endemic diseases ranked according to impact on {ImpactName(rfi.Category)}";

                charts.Add(BuildChart(
                    title,
                    paragraphs,
                    FirstPage(list, RfiChartRows),
                    rfi.Category,
                    isScored: true,
                    axisMin: -100,
                    axisMax: ScoredAxisMax(list.Count > 0 ? RankListOperations.MaxTotalRank(list) : null)));
            }
        }

        AddScoredTotalCharts(items, charts);

        return charts;
    }

    private static void AddScoredTotalCharts(IReadOnlyList<RankItem> items, List<DiseaseRankingChartDto> charts)
    {
        var total = RankListOperations.GetFilteredSortedList(items, null, RankCategory.Total);
        var totalIncludingRisk = RankListOperations.GetFilteredSortedList(items, null, RankCategory.TotalIncludingRisk);

        var totalAxisMax = ScoredAxisMax(total.Count > 0 ? RankListOperations.MaxTotalRank(total) : null);
        var totalIncludingRiskAxisMax = ScoredAxisMax(totalIncludingRisk.Count > 0 ? RankListOperations.MaxTotalIncludingRiskRank(totalIncludingRisk) : null);

        AddContinuedCharts(charts, total, TotalTitle, TotalText, RankCategory.Total, true, -100, totalAxisMax);
        AddContinuedCharts(charts, totalIncludingRisk, TotalIncludingRiskTitle, TotalIncludingRiskText, RankCategory.TotalIncludingRisk, true, -50, totalIncludingRiskAxisMax);
    }

    private static List<DiseaseRankingChartDto> BuildRankedCharts(IReadOnlyList<RankItem> items)
    {
        var charts = new List<DiseaseRankingChartDto>();

        foreach (var rfi in Rfi.All)
        {
            var list = RankListOperations.WithImpact(
                RankListOperations.GetAllSortedList(items, rfi.Category, onePerDisease: true),
                rfi.Category);

            var paragraphs = new List<string> { RankedIntroText };

            if (rfi.Category == RankCategory.InternationalTrade)
            {
                paragraphs.Add(RankedTradeText);
            }

            charts.Add(BuildChart(
                $"Diseases ranked according to potential impact on {ImpactName(rfi.Category)}",
                paragraphs,
                FirstPage(list, RfiChartRows),
                rfi.Category,
                isScored: false,
                axisMin: 0,
                axisMax: list.Count > 0 ? RankedAxisMax : 0));
        }

        // Both lists are built before either is charted, as in the legacy report, because building the
        // second overwrites the percentile ranks the first chart reads.
        var byRisk = RankListOperations.GetAllSortedList(items, RankCategory.Risk);
        var byTotalIncludingRisk = RankListOperations.GetAllSortedList(items, RankCategory.TotalIncludingRisk);

        AddContinuedCharts(charts, byRisk, TotalTitle, TotalText, RankCategory.Total, false, 0, byRisk.Count > 0 ? RankedAxisMax : 0);
        AddContinuedCharts(charts, byTotalIncludingRisk, TotalIncludingRiskTitle, TotalIncludingRiskText, RankCategory.TotalIncludingRisk, false, 0, byTotalIncludingRisk.Count > 0 ? RankedAxisMax : 0);

        return charts;
    }

    private static void AddContinuedCharts(
        List<DiseaseRankingChartDto> charts,
        List<RankItem> ascending,
        string title,
        string text,
        RankCategory category,
        bool isScored,
        double axisMin,
        double axisMax)
    {
        var page = 0;

        foreach (var chunk in RankListOperations.ChunkFromTop(ascending, TotalChartRows))
        {
            charts.Add(BuildChart(
                page == 0 ? title : $"{title} continued...",
                [text],
                chunk,
                category,
                isScored,
                axisMin,
                axisMax));
            page++;
        }
    }

    private static DiseaseRankingChartDto BuildChart(
        string title,
        IReadOnlyList<string> paragraphs,
        IReadOnlyList<RankItem> ascending,
        RankCategory category,
        bool isScored,
        double axisMin,
        double axisMax)
    {
        var series = SeriesFor(category, isScored);

        return new DiseaseRankingChartDto
        {
            Title = title,
            Paragraphs = paragraphs,
            AxisMin = axisMin,
            AxisMax = axisMax,
            AxisInterval = AxisInterval,
            StartsOnNewPage = true,
            Legend = [.. series.Select(entry => new DiseaseRankingSeriesDto { Name = entry.Name, Colour = entry.Colour })],
            // Legacy charts plotted the first item at the bottom, so the highest impact ends up on top.
            Bars = [.. Enumerable.Reverse(ascending).Select(item => BuildBar(item, series))]
        };
    }

    private static DiseaseRankingBarDto BuildBar(RankItem item, IReadOnlyList<Series> series)
    {
        var segments = new List<DiseaseRankingSegmentDto>();
        double negativeEdge = 0;
        double positiveEdge = 0;

        foreach (var entry in series)
        {
            var value = entry.Value(item);

            if (value == 0)
            {
                continue;
            }

            // Stacked bars grow away from the origin in the direction of each value's sign.
            var segment = value < 0
                ? new DiseaseRankingSegmentDto { Series = entry.Name, Colour = entry.Colour, Start = negativeEdge + value, End = negativeEdge }
                : new DiseaseRankingSegmentDto { Series = entry.Name, Colour = entry.Colour, Start = positiveEdge, End = positiveEdge + value };

            negativeEdge = value < 0 ? segment.Start : negativeEdge;
            positiveEdge = value > 0 ? segment.End : positiveEdge;
            segments.Add(segment);
        }

        return new DiseaseRankingBarDto { Label = item.FullTitle, Segments = segments };
    }

    private static List<Series> SeriesFor(RankCategory category, bool isScored)
    {
        var isIncludingRisk = category == RankCategory.TotalIncludingRisk;
        var rfiOrder = RfiOrder(category);

        var risk = isIncludingRisk
            ? new Series("Post mitigation risk", RiskColour, item => item.PositiveRiskRank)
            : isScored
                ? new Series("Post mitigation risk x-1", RiskColour, item => item.RiskRank)
                : new Series("Post mitigation risk", RiskColour, item => item.PmrRank);

        var series = new List<Series> { risk };

        series.AddRange(rfiOrder.Select(rfi => new Series(
            rfi.Name,
            Colour(rfi.Category, isScored),
            isScored || isIncludingRisk ? item => rfi.Rank(item) : rfi.Percentile)));

        return series;
    }

    // The category being charted is stacked first (next to the risk bar), the rest follow in a fixed cycle.
    private static IReadOnlyList<Rfi> RfiOrder(RankCategory category)
    {
        var all = Rfi.All;
        var start = all.ToList().FindIndex(rfi => rfi.Category == category);

        return start <= 0 ? all : [.. all.Skip(start), .. all.Take(start)];
    }

    private static List<RankItem> FirstPage(List<RankItem> ascending, int size) =>
        RankListOperations.ChunkFromTop(ascending, size) is { Count: > 0 } chunks ? chunks[0] : [];

    private static double ScoredAxisMax(int? maxTotal) =>
        maxTotal is null ? 0 : Math.Round((maxTotal.Value + 50) / 50.0) * 50;

    private static string ImpactName(RankCategory category) => category switch
    {
        RankCategory.PublicHealth => "Public Health",
        RankCategory.AnimalWelfare => "Animal Welfare",
        RankCategory.WiderSociety => "Wider Society",
        _ => "International Trade"
    };

    private static string Colour(RankCategory category, bool isScored) => category switch
    {
        RankCategory.InternationalTrade => isScored ? "#FFFF73" : "#FFFF00",
        RankCategory.WiderSociety => isScored ? "#99CCFF" : "#00B2EE",
        RankCategory.AnimalWelfare => isScored ? "#C1FFC1" : "#3D9140",
        RankCategory.PublicHealth => isScored ? "#FD99CC" : "#EE3A8C",
        RankCategory.Risk => RiskColour,
        _ => "#00A33B"
    };

    private sealed record Series(string Name, string Colour, Func<RankItem, double> Value);
}
