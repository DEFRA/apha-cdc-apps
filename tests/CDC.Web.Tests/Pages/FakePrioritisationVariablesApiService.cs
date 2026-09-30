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

    public PrioritisationRankingRangeDto? LastSavedRankingRange { get; private set; }

    public Task<IReadOnlyList<PrioritisationCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_categories);

    public Task UpdateCriterionAsync(
        Guid criterionId,
        int weight,
        IReadOnlyList<CriterionValueScore> valueScores,
        CancellationToken cancellationToken = default)
    {
        if (throwOnUpdateCriterion is not null)
        {
            return Task.FromException(throwOnUpdateCriterion);
        }

        LastUpdatedCriterionId = criterionId;
        LastUpdatedWeight = weight;
        LastUpdatedValueScores = valueScores;
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
}

