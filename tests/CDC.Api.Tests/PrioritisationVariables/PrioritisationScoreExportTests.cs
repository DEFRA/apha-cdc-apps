using System.Text;
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
        result.Value.FileName.Should().Be("Welfare.csv");
        var lines = ReadLines(result.Value.Content);
        lines.Should().HaveCount(3);
        lines[0].Should().Be(
            "\"Disease / Issue\",\"Version number\",\"Profiled species\",\"Published / Draft\",\"Profile status\",\"Standard / Scenario\"," +
            "\"Zoonotic (2.1)\",\"Welfare impact (6.1)\",\"Weighted score for 6.1\",\"Normalised score\"");
        lines[1].Should().Be("\"Bovine TB\",\"v1.0\",\"Cattle\",\"Published\",\"Live\",\"Standard\",\"Yes\",\"High\",\"30\",\"75\"");
        lines[2].Should().Be("\"Bovine TB (Vaccination)\",\"v0.1\",\"Cattle\",\"Draft\",\"Live\",\"Scenario\",\"\",\"\",\"\",\"\"");
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
        result.Value.FileName.Should().Be("NormalisedScoreSummary.csv");
        var lines = ReadLines(result.Value.Content);
        lines[0].Should().Contain("\"Welfare normalised score\",\"Welfare rank\",\"Other normalised score\",\"Other rank\"");
        lines[1].Should().EndWith("\"75\",\"1\",\"\",\"\",\"1\",\"1\"");
        lines[2].Should().EndWith("\"\",\"\",\"\",\"\",\"\",\"\"");
    }

    [Fact]
    public async Task Export_NeutralisesSpreadsheetFormulasAndStripsMarkup()
    {
        var data = CreateScoreData() with
        {
            Profiles = [new ScoreDataProfile(ProfileId, "=HYPERLINK(\"x\") <i>TB</i> &amp;", "Live")]
        };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        ReadLines(result.Value.Content)[1].Should().StartWith("\"'=HYPERLINK(\"\"x\"\") TB &\",");
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

        var lines = ReadLines(result.Value.Content);
        lines[1].Should().Contain("\"\",\"1\",\"\",\"1\",\"\",\"1\",\"\",\"1\",\"\",\"1\",\"\",\"\"");
    }

    [Fact]
    public async Task ExportCategoryScoreData_UsesADefaultFileName_WhenTheCategoryNameHasNoSafeCharacters()
    {
        var data = CreateScoreData() with { Categories = [new ScoreDataCategory(WelfareId, "***")] };
        repository.Setup(repo => repo.GetScoreDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateHandler().Handle(new ExportCategoryScoreDataQuery(WelfareId), CancellationToken.None);

        result.Value.FileName.Should().Be("ScoreData.csv");
    }

    private ExportPrioritisationScoresQueryHandler CreateHandler() => new(repository.Object, rankingRepository.Object);

    private static string[] ReadLines(byte[] content)
    {
        content.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());

        return Encoding.UTF8.GetString(content, 3, content.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

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
