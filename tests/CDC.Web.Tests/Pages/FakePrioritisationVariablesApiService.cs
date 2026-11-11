using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for IPrioritisationVariablesApiService so page-model tests don't need a real HTTP call.
internal sealed class FakePrioritisationVariablesApiService(
    IReadOnlyList<PrioritisationCategoryDto>? categories = null,
    Exception? throwOnUpdateCriterion = null,
    PrioritisationRankingRangeDto? rankingRange = null,
    Exception? throwOnUpdateRankingRange = null)
    : IPrioritisationVariablesApiService
{
    private readonly IReadOnlyList<PrioritisationCategoryDto> _categories = categories ?? [];

    private PrioritisationRankingRangeDto _rankingRange = rankingRange
        ?? new PrioritisationRankingRangeDto { LowerBound = 0, UpperBound = 100, RowVersion = "AQIDBAUGBwg=" };

    public Guid? LastUpdatedCriterionId { get; private set; }

    public int? LastUpdatedWeight { get; private set; }

    public IReadOnlyList<CriterionValueScore>? LastUpdatedValueScores { get; private set; }

    public string? LastUpdatedName { get; private set; }

    public PrioritisationRankingRangeDto? LastSavedRankingRange { get; private set; }

    public Task<IReadOnlyList<PrioritisationCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        ThrowOnGetCategories is not null
            ? Task.FromException<IReadOnlyList<PrioritisationCategoryDto>>(ThrowOnGetCategories)
            : Task.FromResult(_categories);

    public Exception? ThrowOnGetCategories { get; init; }

    public Task UpdateCriterionAsync(
        Guid criterionId,
        int weight,
        IReadOnlyList<CriterionValueScore> valueScores,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        if (throwOnUpdateCriterion is not null)
        {
            return Task.FromException(throwOnUpdateCriterion);
        }

        LastUpdatedCriterionId = criterionId;
        LastUpdatedWeight = weight;
        LastUpdatedValueScores = valueScores;
        LastUpdatedName = name;
        return Task.CompletedTask;
    }

    public Task<PrioritisationRankingRangeDto> GetRankingRangeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_rankingRange);

    public Task<PrioritisationRankingRangeDto> UpdateRankingRangeAsync(
        PrioritisationRankingRangeDto rankingRange,
        CancellationToken cancellationToken = default)
    {
        if (throwOnUpdateRankingRange is not null)
        {
            return Task.FromException<PrioritisationRankingRangeDto>(throwOnUpdateRankingRange);
        }

        LastSavedRankingRange = rankingRange;
        _rankingRange = rankingRange with { RowVersion = "CQoLDA0ODxA=" };
        return Task.FromResult(_rankingRange);
    }

    public Exception? ThrowOnPrioritiseDraftProfiles { get; init; }

    public Exception? ThrowOnRecalculateScores { get; init; }

    public Exception? ThrowOnExport { get; init; }

    public int DraftPrioritisationCount { get; private set; }

    public int RecalculationCount { get; private set; }

    public Task PrioritiseDraftProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnPrioritiseDraftProfiles is not null)
        {
            return Task.FromException(ThrowOnPrioritiseDraftProfiles);
        }

        DraftPrioritisationCount++;
        return Task.CompletedTask;
    }

    public Task RecalculateScoresAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnRecalculateScores is not null)
        {
            return Task.FromException(ThrowOnRecalculateScores);
        }

        RecalculationCount++;
        return Task.CompletedTask;
    }

    public Task<PrioritisationScoreExport?> ExportCategoryScoreDataAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnExport is not null)
        {
            return Task.FromException<PrioritisationScoreExport?>(ThrowOnExport);
        }

        return Task.FromResult(_categories.Any(category => category.Id == categoryId)
            ? new PrioritisationScoreExport("a,b"u8.ToArray(), "Category.csv")
            : null);
    }

    public Task<PrioritisationScoreExport> ExportNormalisedScoresAsync(CancellationToken cancellationToken = default) =>
        ThrowOnExport is not null
            ? Task.FromException<PrioritisationScoreExport>(ThrowOnExport)
            : Task.FromResult(new PrioritisationScoreExport("a,b"u8.ToArray(), "NormalisedScoreSummary.csv"));
}

