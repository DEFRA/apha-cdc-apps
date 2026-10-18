using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;
using CDC.Common.Contracts;
using FluentAssertions;

namespace CDC.Api.Tests.DiseaseRanking;

public class DiseaseRankingReportBuilderTests
{
    private static readonly DiseaseRankingScope Terrestrial = new("Terrestrial Diseases", "Terrestrial");

    private static RankedProfile Profile(
        string title,
        bool isExotic,
        int publicHealth = 0,
        int animalWelfare = 0,
        int widerSociety = 0,
        int internationalTrade = 0,
        int risk = 0,
        bool awaitingValidation = false,
        string species = "Cattle",
        Guid? profileId = null) => new()
        {
            ProfileId = profileId ?? Guid.NewGuid(),
            SpeciesId = Guid.NewGuid(),
            ProfileTitle = title,
            SpeciesName = species,
            IsExotic = isExotic,
            IsAwaitingValidation = awaitingValidation,
            PublicHealthRank = publicHealth,
            AnimalWelfareRank = animalWelfare,
            WiderSocietyRank = widerSociety,
            InternationalTradeRank = internationalTrade,
            RiskRank = risk
        };

    private static List<RankedProfile> SampleProfiles() =>
    [
        Profile("Anthrax", isExotic: true, publicHealth: 10, risk: 5),
        Profile("Brucellosis", isExotic: false, publicHealth: 20, animalWelfare: 5, risk: 2),
        Profile("Cowpox", isExotic: true, animalWelfare: 7, internationalTrade: 3, risk: 7)
    ];

    [Fact]
    public void Build_Scored_SetsHeadingsFooterAndFileName()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, new DiseaseRankingScope("Viral/Pig <diseases>", "Viral/Pig <diseases>"), SampleProfiles());

        report.Kind.Should().Be(DiseaseRankingReportKind.Scored);
        report.Title.Should().Be("Issues ranked by impact on each of the four reasons for government intervention for Viral/Pig <diseases>");
        report.FooterTitle.Should().Be("RFI scoring report for Viral/Pig <diseases>.");
        report.FileName.Should().Be("DiseaseRankingReport_Scored_ViralPig diseases");
        report.Subtitle.Should().StartWith("This report presents a table of rankings");
    }

    [Fact]
    public void Build_Ranked_SetsRankedFooter()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Ranked, Terrestrial, SampleProfiles());

        report.FooterTitle.Should().Be("Disease ranking report for Terrestrial.");
        report.FileName.Should().Be("DiseaseRankingReport_Ranked_Terrestrial");
    }

    [Fact]
    public void Build_BuildsOneTopTablePerReasonForIntervention_HighestRankFirst()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles());

        report.TopTables.Select(table => table.Name).Should().Equal("PUBLIC HEALTH", "ANIMAL WELFARE", "WIDER SOCIETY", "INTERNATIONAL TRADE");

        var publicHealth = report.TopTables[0];
        publicHealth.Colour.Should().Be("#FD99CC");
        publicHealth.Rows.Select(row => row.Disease).Should().Equal("Brucellosis (Cattle)", "ANTHRAX (Cattle)");
        publicHealth.Rows.Select(row => row.Rank).Should().Equal("1", "2");
        publicHealth.Rows[0].Score.Should().Be(20);
        publicHealth.Rows[0].Pmr.Should().Be(2);
        publicHealth.Rows[0].IsExotic.Should().BeFalse();
        publicHealth.Rows[1].IsExotic.Should().BeTrue();
    }

    [Fact]
    public void Build_TopTable_MarksTiedRanksWithEqualsSign()
    {
        List<RankedProfile> profiles =
        [
            Profile("Alpha", isExotic: false, publicHealth: 10, risk: 1),
            Profile("Beta", isExotic: false, publicHealth: 10, risk: 1),
            Profile("Gamma", isExotic: false, publicHealth: 5, risk: 1)
        ];

        var rows = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).TopTables[0].Rows;

        rows.Select(row => row.Rank).Should().Equal("1=", "1=", "3");
    }

    [Fact]
    public void Build_TopTable_RanksLowerRiskBehindHigherRiskAtTheSameScore()
    {
        List<RankedProfile> profiles =
        [
            Profile("Alpha", isExotic: false, publicHealth: 10, risk: 5),
            Profile("Beta", isExotic: false, publicHealth: 10, risk: 1)
        ];

        var rows = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).TopTables[0].Rows;

        rows.Select(row => row.Rank).Should().Equal("1", "2");
        rows[0].Pmr.Should().Be(5);
    }

    [Fact]
    public void Build_TopTable_IsLimitedToTwentyFiveRows()
    {
        var profiles = Enumerable.Range(1, 40).Select(index => Profile($"Disease {index}", isExotic: false, publicHealth: index)).ToList();

        var rows = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).TopTables[0].Rows;

        rows.Should().HaveCount(25);
        rows[0].Score.Should().Be(40);
    }

    [Fact]
    public void Build_TopTable_KeepsOnlyTheBestSpeciesForEachProfile()
    {
        var profileId = Guid.NewGuid();
        List<RankedProfile> profiles =
        [
            Profile("Anthrax", isExotic: false, publicHealth: 4, species: "Sheep", profileId: profileId),
            Profile("Anthrax", isExotic: false, publicHealth: 9, species: "Cattle", profileId: profileId)
        ];

        var rows = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).TopTables[0].Rows;

        rows.Should().ContainSingle().Which.Disease.Should().Be("Anthrax (Cattle)");
    }

    [Fact]
    public void Build_Scored_BuildsExoticAndEndemicChartsForEachReasonThenTheTotalCharts()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles());

        report.Charts.Should().HaveCount(10);
        report.Charts[0].Title.Should().Be("Exotic diseases ranked according to potential impact on Public Health");
        report.Charts[1].Title.Should().Be("Endemic diseases ranked according to impact on Public Health");
        report.Charts[7].Title.Should().Be("Endemic diseases ranked according to impact on International Trade");
        report.Charts[8].Title.Should().Be("Diseases ranked by total relative impact on the four reasons for government intervention");
        report.Charts[9].Title.Should().Be("Diseases ranked by post mitigation risk plus total relative impact on the four reasons for government intervention");
        report.Charts.Should().OnlyContain(chart => chart.StartsOnNewPage && chart.AxisInterval == 50);
    }

    [Fact]
    public void Build_Scored_DrawsRiskLeftOfTheAxisAndTheReasonRightOfIt()
    {
        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles()).Charts[0];

        chart.Legend.Select(entry => entry.Name).Should().Equal(
            "Post mitigation risk x-1", "Public health", "Welfare", "Wider society", "International trade");

        var bar = chart.Bars.Should().ContainSingle().Subject;
        bar.Label.Should().Be("ANTHRAX (CATTLE)");
        bar.Segments.Should().HaveCount(2);
        bar.Segments[0].Series.Should().Be("Post mitigation risk x-1");
        (bar.Segments[0].Start, bar.Segments[0].End).Should().Be((-5, 0));
        bar.Segments[0].Colour.Should().Be("#000000");
        (bar.Segments[1].Start, bar.Segments[1].End).Should().Be((0, 10));
        bar.Segments[1].Colour.Should().Be("#FD99CC");
    }

    [Fact]
    public void Build_Scored_SizesTheAxisFromTheHighestTotal()
    {
        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles()).Charts[0];

        chart.AxisMin.Should().Be(-100);
        chart.AxisMax.Should().Be(50);
    }

    [Fact]
    public void Build_Scored_LeavesTheAxisAtZeroWhenAChartHasNoDiseases()
    {
        List<RankedProfile> profiles = [Profile("Anthrax", isExotic: true, publicHealth: 10, risk: 5)];

        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).Charts[1];

        chart.Bars.Should().BeEmpty();
        chart.AxisMax.Should().Be(0);
    }

    [Fact]
    public void Build_Scored_AddsTheTradeExplanationOnlyToTheEndemicInternationalTradeChart()
    {
        var charts = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles()).Charts;

        charts[6].Paragraphs.Should().ContainSingle();
        charts[7].Paragraphs.Should().HaveCount(2);
        charts[7].Paragraphs[1].Should().StartWith("The Animal Health and Welfare Strategy");
    }

    [Fact]
    public void Build_Scored_TotalChartsRankEveryProfileHighestTotalFirst()
    {
        var charts = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, SampleProfiles()).Charts;

        charts[8].Bars.Select(bar => bar.Label).Should().Equal("Brucellosis (Cattle)", "COWPOX (CATTLE)", "ANTHRAX (CATTLE)");
        charts[8].AxisMin.Should().Be(-100);
        charts[9].AxisMin.Should().Be(-50);
        charts[9].Legend[0].Name.Should().Be("Post mitigation risk");
        charts[9].Bars[0].Segments[0].Series.Should().Be("Post mitigation risk");
        charts[9].Bars[0].Segments[0].Start.Should().Be(0);
    }

    [Fact]
    public void Build_Scored_SplitsLongTotalListsIntoSixtyRowPages()
    {
        var profiles = Enumerable.Range(1, 61).Select(index => Profile($"Disease {index:00}", isExotic: false, publicHealth: index)).ToList();

        var charts = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).Charts;

        var totalPages = charts.Where(chart => chart.Title.StartsWith("Diseases ranked by total", StringComparison.Ordinal)).ToList();
        totalPages.Should().HaveCount(2);
        totalPages[0].Bars.Should().HaveCount(60);
        totalPages[0].Title.Should().NotContain("continued");
        totalPages[1].Bars.Should().ContainSingle();
        totalPages[1].Title.Should().EndWith("continued...");
    }

    [Fact]
    public void Build_Scored_LimitsEachReasonChartToTheTopFifty()
    {
        var profiles = Enumerable.Range(1, 70).Select(index => Profile($"Disease {index:00}", isExotic: false, publicHealth: index)).ToList();

        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).Charts[1];

        chart.Bars.Should().HaveCount(50);
        chart.Bars[0].Label.Should().Be("Disease 70 (Cattle)");
    }

    [Fact]
    public void Build_Ranked_BuildsOneChartPerReasonThenTheTotalCharts()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Ranked, Terrestrial, SampleProfiles());

        report.Charts.Should().HaveCount(6);
        report.Charts[0].Title.Should().Be("Diseases ranked according to potential impact on Public Health");
        report.Charts[3].Title.Should().Be("Diseases ranked according to potential impact on International Trade");
        report.Charts[3].Paragraphs.Should().HaveCount(2);
        report.Charts[3].Paragraphs[1].Should().EndWith("contains very few (if any) endemic diseases.");
        report.Charts[0].Paragraphs.Should().ContainSingle();
    }

    [Fact]
    public void Build_Ranked_StacksPostMitigationRiskThenPercentileRanks()
    {
        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Ranked, Terrestrial, SampleProfiles()).Charts[0];

        chart.AxisMin.Should().Be(0);
        chart.AxisMax.Should().Be(500);
        chart.Legend.Select(entry => entry.Name).Should().Equal(
            "Post mitigation risk", "Public health", "Welfare", "Wider society", "International trade");
        chart.Legend[1].Colour.Should().Be("#EE3A8C");

        // Brucellosis: highest public health percentile, so it is drawn on top.
        chart.Bars.Select(bar => bar.Label).Should().Equal("Brucellosis (Cattle)", "ANTHRAX (CATTLE)");

        var brucellosis = chart.Bars[0].Segments;
        brucellosis[0].Series.Should().Be("Post mitigation risk");
        brucellosis[0].End.Should().BeApproximately(100d / 3, 0.001);
        brucellosis[1].Series.Should().Be("Public health");
        brucellosis[1].Start.Should().BeApproximately(100d / 3, 0.001);
        brucellosis[1].End.Should().BeApproximately(100d + (100d / 3), 0.001);
    }

    [Fact]
    public void Build_Ranked_LeavesTheAxisAtZeroWhenAChartHasNoDiseases()
    {
        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Ranked, Terrestrial, []).Charts[0];

        chart.Bars.Should().BeEmpty();
        chart.AxisMax.Should().Be(0);
    }

    [Fact]
    public void Build_WithNoProfiles_StillBuildsTheFrontPageTables()
    {
        var report = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, []);

        report.TopTables.Should().HaveCount(4).And.OnlyContain(table => table.Rows.Count == 0);
    }

    [Fact]
    public void Build_LabelsFlagUnvalidatedProfilesStripMarkupAndTruncate()
    {
        List<RankedProfile> profiles =
        [
            Profile("<p>Foot &amp; mouth</p>", isExotic: true, publicHealth: 3, awaitingValidation: true, species: "Cattle"),
            Profile(new string('x', 80), isExotic: false, publicHealth: 2)
        ];

        var chart = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).Charts[0];

        chart.Bars.Should().ContainSingle().Which.Label.Should().Be("*FOOT & MOUTH (CATTLE)");

        var endemicRow = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).Charts[1].Bars.Single();
        endemicRow.Label.Should().HaveLength(50);
    }

    [Fact]
    public void Build_TopTableLabelsKeepSpeciesNamesAsEnteredForExoticDiseases()
    {
        List<RankedProfile> profiles =
        [
            Profile("Foot and mouth", isExotic: true, publicHealth: 3, awaitingValidation: true, species: "Cattle"),
            Profile("Anthrax", isExotic: false, publicHealth: 2, awaitingValidation: true, species: "Sheep")
        ];

        var rows = DiseaseRankingReportBuilder.Build(DiseaseRankingReportKind.Scored, Terrestrial, profiles).TopTables[0].Rows;

        rows.Select(row => row.Disease).Should().Equal("*FOOT AND MOUTH (Cattle)", "*Anthrax (Sheep)");
    }
}
