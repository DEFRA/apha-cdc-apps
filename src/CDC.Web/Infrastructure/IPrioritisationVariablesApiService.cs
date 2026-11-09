using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the prioritisation variables endpoints on CDC.Api.
/// </summary>
public interface IPrioritisationVariablesApiService
{
    /// <summary>Calls <c>GET /api/prioritisation-variables/categories</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every prioritisation category, each with its criteria.</returns>
    Task<IReadOnlyList<PrioritisationCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/prioritisation-variables/criteria/{criterionId}</c>.</summary>
    /// <param name="criterionId">The criterion to update.</param>
    /// <param name="weight">The new weighting.</param>
    /// <param name="valueScores">The new score for every value belonging to the criterion.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task UpdateCriterionAsync(
        Guid criterionId,
        int weight,
        IReadOnlyList<CriterionValueScore> valueScores,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/prioritisation-variables/ranking-range</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current ranking range and its row version.</returns>
    Task<PrioritisationRankingRangeDto> GetRankingRangeAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/prioritisation-variables/ranking-range</c>.</summary>
    /// <param name="rankingRange">The new bounds and the row version read alongside them.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved range with its new row version.</returns>
    /// <exception cref="RankingRangeConflictException">
    /// Thrown when another user has saved a change since the row version was read.
    /// </exception>
    Task<PrioritisationRankingRangeDto> UpdateRankingRangeAsync(PrioritisationRankingRangeDto rankingRange, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/prioritisation-variables/draft-prioritisation</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task PrioritiseDraftProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/prioritisation-variables/score-recalculation</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task RecalculateScoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/prioritisation-variables/categories/{categoryId}/score-data</c>.</summary>
    /// <param name="categoryId">The category to export.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The CSV file, or <see langword="null"/> when the category does not exist.</returns>
    Task<PrioritisationScoreExport?> ExportCategoryScoreDataAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/prioritisation-variables/normalised-scores</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The CSV file.</returns>
    Task<PrioritisationScoreExport> ExportNormalisedScoresAsync(CancellationToken cancellationToken = default);
}

/// <summary>A CSV score export downloaded from CDC.Api.</summary>
/// <param name="Content">The CSV bytes.</param>
/// <param name="FileName">The download file name.</param>
public sealed record PrioritisationScoreExport(byte[] Content, string FileName);

/// <summary>
/// Thrown when <c>PUT /api/prioritisation-variables/ranking-range</c> returns 409 Conflict
/// because another user saved a change since the row version was read.
/// </summary>
public sealed class RankingRangeConflictException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="RankingRangeConflictException"/> class.</summary>
    /// <param name="message">Description of the conflict.</param>
    public RankingRangeConflictException(string message) : base(message)
    {
    }
}
