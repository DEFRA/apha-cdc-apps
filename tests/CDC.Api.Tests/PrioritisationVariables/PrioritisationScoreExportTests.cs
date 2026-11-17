using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.PrioritisationVariables;
using CDC.Api.Features.PrioritisationVariables.Commands;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationScoreExportTests
{
    private static readonly Guid WelfareId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CommonQuestionId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid WelfareQuestionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ProfileId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid PublishedVersionId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid ScenarioVersionId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid CattleId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private readonly Mock<IPrioritisationVariablesRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IDiseaseRankingRepository> rankingRepository = new(MockBehavior.Strict);

    [Fact]
    public async Task PrioritiseDraftProfilesCommandHandler_RecalculatesTheDraftProfiles()
    {
        repository.Setup(repo => repo.PrioritiseDraftProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await new PrioritiseDraftProfilesCommandHandler(repository.Object, NullLogger<PrioritiseDraftProfilesCommandHandler>.Instance)
            .Handle(new PrioritiseDraftProfilesCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.PrioritiseDraftProfilesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculatePrioritisationScoresCommandHandler_RecalculatesTheScores()
    {
        repository.Setup(repo => repo.RecalculatePrioritisationScoresAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new RecalculatePrioritisationScoresCommandHandler(repository.Object, NullLogger<RecalculatePrioritisationScoresCommandHandler>.Instance)
            .Handle(new RecalculatePrioritisationScoresCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.RecalculatePrioritisationScoresAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportCategoryScoreData_WritesTheLegacyColumnsAndOneRowPerVersionAndSpecies()
    {
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateScoreData());

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("Welfare.xls");
        var rows = ReadRows(result.Value.Content);
        rows.Should().HaveCount(3);
        rows[0].Should().Equal(
            "Disease / Issue", "Version number", "Profiled species", "Published / Draft", "Profile status", "Standard / Scenario",
            "Zoonotic (2.1)", "Welfare impact (6.1)", "Weighted score for 6.1", "Normalised score");
        rows[1].Should().Equal("Bovine TB", "v1.0", "Cattle", "Published", "Live", "Standard", "Yes", "High", "30", "75");
        rows[2].Should().Equal("Bovine TB (Vaccination)", "v0.1", "Cattle", "Draft", "Live", "Scenario", "", "", "", "");
    }

    [Fact]
    public async Task ExportCategoryScoreData_ColoursEveryHeaderWithTheCategoryColour()
    {
        var data = CreateScoreData() with { Categories = [new ScoreDataCategory(WelfareId, "Post mitigation risk")] };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        var headers = ReadHeaderStyles(result.Value.Content);
        headers.Should().OnlyContain(style =>
            style.Contains("background-color:rgb(0,0,0)") && style.Contains("color:White") && style.Contains("font-weight:bold"));
    }

    [Fact]
    public async Task ExportCategoryScoreData_ReturnsNotFound_ForAnUnknownCategory()
    {
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateScoreData());

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExportNormalisedScores_RanksStandardVersionsOnlyAndLeavesUnrankedCategoriesBlank()
    {
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateScoreData());
        rankingRepository.Setup(repo => repo.GetRankedProfilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new RankedProfile
            {
                ProfileId = ProfileId,
                SpeciesId = CattleId,
                ProfileTitle = "Bovine TB",
                SpeciesName = "Cattle",
                IsExotic = false,
                IsAwaitingValidation = false,
                AnimalWelfareRank = 75,
                AnimalWelfareProfileVersionId = PublishedVersionId
            }
        ]);

        var result = await CreateHandler().Handle(new ExportNormalisedScoresQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("NormalisedScoreSummary.xls");
        var rows = ReadRows(result.Value.Content);
        string.Join("|", rows[0]).Should().Contain("Welfare normalised score|Welfare rank|Other normalised score|Other rank");
        rows[1].TakeLast(6).Should().Equal("75", "1", "", "", "1", "1");
        rows[2].TakeLast(6).Should().Equal("", "", "", "", "", "");
    }

    [Fact]
    public async Task ExportNormalisedScores_ColoursTheHeadersByCategoryAndKeepsTheTotalsGrey()
    {
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateScoreData());
        rankingRepository.Setup(repo => repo.GetRankedProfilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateHandler().Handle(new ExportNormalisedScoresQuery(), CancellationToken.None);

        var colours = ReadHeaderStyles(result.Value.Content)
            .Select(style => Regex.Match(style, "background-color:(rgb\\([^)]*\\))").Groups[1].Value)
            .ToList();
        const string green = "rgb(193,255,193)";
        const string olive = "rgb(102,102,0)";
        const string grey = "rgb(200,200,200)";
        colours.Should().Equal([.. Enumerable.Repeat(grey, 7), green, green, olive, olive, grey, grey]);
    }

    [Fact]
    public async Task Export_ForcesFormulaLookingCellsToTextAndStripsMarkup()
    {
        var data = CreateScoreData() with
        {
            Profiles = [new ScoreDataProfile(ProfileId, "=HYPERLINK(\"x\") <i>TB</i> &amp;", "Live")]
        };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        var html = Encoding.UTF8.GetString(result.Value.Content);
        html.Should().Contain("mso-number-format:'\\@';");
        ReadRows(result.Value.Content)[1][0].Should().Be("=HYPERLINK(\"x\") TB &");
    }

    [Fact]
    public async Task Export_KeepsItalicsAndDropsOtherMarkup_InTitles()
    {
        var data = CreateScoreData() with
        {
            Profiles = [new ScoreDataProfile(ProfileId, "Bovine <em>Brucella</em> <b>abortus</b> &amp; <script>x</script>", "Live")]
        };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        ReadHtml(result.Value.Content).Should().Contain(">Bovine <i>Brucella</i> abortus &amp; x</td>");
    }

    [Fact]
    public async Task Export_BottomAlignsEveryCellAsTheLegacyTableCellsWere()
    {
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateScoreData());

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        var html = ReadHtml(result.Value.Content);
        html.Should().NotContain("<th");
        Regex.Matches(html, "<td style=\"[^\"]*\"").Should().OnlyContain(cell => cell.Value.Contains("vertical-align:bottom"));
    }

    [Fact]
    public async Task ExportNormalisedScores_RanksEveryKnownCategoryAndLeavesOthersBlank()
    {
        var data = CreateScoreData() with
        {
            Categories =
            [
                new ScoreDataCategory(Guid.NewGuid(), "International trade"),
                new ScoreDataCategory(Guid.NewGuid(), "Public health"),
                new ScoreDataCategory(Guid.NewGuid(), "Welfare"),
                new ScoreDataCategory(Guid.NewGuid(), "Wider society"),
                new ScoreDataCategory(Guid.NewGuid(), "Post mitigation risk"),
                new ScoreDataCategory(Guid.NewGuid(), "Unmapped")
            ]
        };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);
        rankingRepository.Setup(repo => repo.GetRankedProfilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new RankedProfile
            {
                ProfileId = ProfileId,
                SpeciesId = CattleId,
                ProfileTitle = "Bovine TB",
                SpeciesName = "Cattle",
                IsExotic = false,
                IsAwaitingValidation = false,
                InternationalTradeRank = 10,
                InternationalTradeProfileVersionId = PublishedVersionId,
                PublicHealthRank = 20,
                PublicHealthProfileVersionId = PublishedVersionId,
                AnimalWelfareRank = 30,
                AnimalWelfareProfileVersionId = PublishedVersionId,
                WiderSocietyRank = 40,
                WiderSocietyProfileVersionId = PublishedVersionId,
                RiskRank = 50,
                RiskProfileVersionId = PublishedVersionId
            }
        ]);

        var result = await CreateHandler().Handle(new ExportNormalisedScoresQuery(), CancellationToken.None);

        var rows = ReadRows(result.Value.Content);
        rows[1].Skip(6 + 1).Take(12).Should().Equal("", "1", "", "1", "", "1", "", "1", "", "1", "", "");
    }

    [Fact]
    public async Task ExportCategoryScoreData_UsesADefaultFileName_WhenTheCategoryNameHasNoSafeCharacters()
    {
        var data = CreateScoreData() with { Categories = [new ScoreDataCategory(WelfareId, "***")] };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        result.Value.FileName.Should().Be("ScoreData.xls");
    }

    private ExportPrioritisationScoresQueryHandler CreateHandler() => new(repository.Object, rankingRepository.Object);

    private static string ReadHtml(byte[] content)
    {
        content.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());

        return Encoding.UTF8.GetString(content, 3, content.Length - 3);
    }

    private static List<string> ReadHeaderStyles(byte[] content) =>
        [.. Regex.Matches(Regex.Match(ReadHtml(content), "<tr>(.*?)</tr>", RegexOptions.Singleline).Groups[1].Value, "<td style=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value)];

    private static List<List<string>> ReadRows(byte[] content) =>
        [.. Regex.Matches(ReadHtml(content), "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(row =>
            Regex.Matches(row.Groups[1].Value, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline)
                .Select(cell => WebUtility.HtmlDecode(Regex.Replace(cell.Groups[1].Value, "<span.*?</span>|</?i>", string.Empty)))
                .ToList())];

    private static PrioritisationScoreData CreateScoreData() => new()
    {
        CommonQuestions = [new ScoreDataCommonQuestion(CommonQuestionId, "2.1", "Zoonotic")],
        Categories = [new ScoreDataCategory(WelfareId, "Welfare"), new ScoreDataCategory(OtherCategoryId, "Other")],
        PrioritisationQuestions = [new ScoreDataPrioritisationQuestion(WelfareQuestionId, WelfareId, "6.1", "Welfare impact", IsPerSpecies: true)],
        Profiles = [new ScoreDataProfile(ProfileId, "Bovine TB", "Live")],
        ProfileVersions =
        [
            new ScoreDataProfileVersion(PublishedVersionId, ProfileId, "Current situation", "v1.0", "Published", IsScenario: false),
            new ScoreDataProfileVersion(ScenarioVersionId, ProfileId, "Vaccination", "v0.1", "Draft", IsScenario: true)
        ],
        ProfiledSpecies =
        [
            new ScoreDataProfiledSpecies(CattleId, PublishedVersionId, "Cattle"),
            new ScoreDataProfiledSpecies(CattleId, ScenarioVersionId, "Cattle")
        ],
        CommonAnswers = [new ScoreDataCommonAnswer(CommonQuestionId, PublishedVersionId, "Yes")],
        PrioritisationAnswers = [new ScoreDataPrioritisationAnswer(WelfareQuestionId, PublishedVersionId, CattleId, "High", Score: 3, Weighting: 10)],
        NormalisedScores = [new ScoreDataNormalisedScore(PublishedVersionId, CattleId, WelfareId, Score: 30, Rank: 75)]
    };
}
