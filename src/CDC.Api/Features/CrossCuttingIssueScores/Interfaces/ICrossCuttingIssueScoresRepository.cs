using CDC.Api.Features.CrossCuttingIssueScores.Dtos;

namespace CDC.Api.Features.CrossCuttingIssueScores.Interfaces;

/// <summary>
/// Data access for cross-cutting issue scores. Implementations must contain no business logic.
/// </summary>
public interface ICrossCuttingIssueScoresRepository
{
    /// <summary>Reads every issue, criterion and scored value via <c>spgaSpeciesPrioritisationVariables</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every issue, each with its criteria and values in sequence order.</returns>
    Task<IReadOnlyList<CrossCuttingIssueCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the supplied scores via <c>spuSpeciesPrioritisationCriterionValue</c>, then runs
    /// <c>sppSpeciesPrioritisationScore</c>, all in one transaction.
    /// </summary>
    /// <param name="scores">Criterion value id to new score.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateScoresAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken);
}
