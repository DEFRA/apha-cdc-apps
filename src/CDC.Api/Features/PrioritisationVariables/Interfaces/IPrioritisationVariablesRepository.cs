using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.PrioritisationVariables.Interfaces;

/// <summary>
/// Data access for prioritisation categories and their criteria. Implementations must contain
/// no business logic.
/// </summary>
public interface IPrioritisationVariablesRepository
{
    /// <summary>Reads every category with its criteria via <c>spgaPrioritisationVariables</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every category, each with its criteria in code order.</returns>
    Task<IReadOnlyList<PrioritisationCategory>> GetCategoriesWithCriteriaAsync(CancellationToken cancellationToken);

    /// <summary>Updates a criterion's name and weighting via <c>spuPrioritisationCriterion</c>.</summary>
    /// <param name="criterionId">The criterion to update.</param>
    /// <param name="name">The criterion name - unchanged unless the caller is also renaming it.</param>
    /// <param name="weight">The new weighting.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateCriterionAsync(Guid criterionId, string name, int weight, CancellationToken cancellationToken);

    /// <summary>Updates a criterion value's score via <c>spuPrioritisationCriterionValue</c>.</summary>
    /// <param name="criterionValueId">The criterion value to update.</param>
    /// <param name="score">The new score.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateCriterionValueScoreAsync(Guid criterionValueId, int score, CancellationToken cancellationToken);

    /// <summary>Reads the ranking range via <c>spgaPrioritisationVariables</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The current ranking range and its row version.</returns>
    Task<PrioritisationRankingRange> GetRankingRangeAsync(CancellationToken cancellationToken);

    /// <summary>Updates the ranking range via <c>spuPrioritisationRankingRange</c>.</summary>
    /// <param name="lowerBound">The new lower bound.</param>
    /// <param name="upperBound">The new upper bound.</param>
    /// <param name="rowVersion">The row version read alongside the values being replaced.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new row version.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task<byte[]> UpdateRankingRangeAsync(int lowerBound, int upperBound, byte[] rowVersion, CancellationToken cancellationToken);
}
