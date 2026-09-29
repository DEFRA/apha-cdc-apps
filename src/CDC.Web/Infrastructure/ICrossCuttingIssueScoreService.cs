using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Provides read and update access to cross-cutting issue criterion-value scores.
/// </summary>
/// <remarks>
/// Backed by an in-memory store pending a dedicated CDC.Api endpoint. Mirrors the legacy
/// <c>SpeciesPrioritisationMetadata</c> aggregate: load the whole category/criterion/value graph,
/// then commit all changed scores in one call which also triggers recalculation.
/// </remarks>
public interface ICrossCuttingIssueScoreService
{
    /// <summary>Returns every cross-cutting issue with its criteria and their scored values.</summary>
    Task<IReadOnlyList<CrossCuttingIssueCategory>> GetMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the supplied criterion-value scores, then recalculates every cross-cutting issue
    /// score. Equivalent to the legacy <c>Save</c> followed by <c>sppSpeciesPrioritisationScore</c>.
    /// </summary>
    /// <param name="scores">Criterion-value id to new score, for changed values only.</param>
    Task SaveAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken = default);
}
