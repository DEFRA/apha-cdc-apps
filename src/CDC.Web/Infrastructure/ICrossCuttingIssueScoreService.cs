using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Provides read and update access to cross-cutting issue prioritisation scores.
/// </summary>
/// <remarks>
/// Backed by an in-memory store pending a dedicated CDC.Api endpoint. The interface shape
/// mirrors the typed API clients (see <see cref="ISpeciesApiService"/>) so it can be swapped for
/// an HTTP-backed implementation later without changing the page model.
/// </remarks>
public interface ICrossCuttingIssueScoreService
{
    /// <summary>Returns every cross-cutting issue and its current score.</summary>
    Task<IReadOnlyList<CrossCuttingIssueScoreDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single cross-cutting issue, or <see langword="null"/> if the id is not recognised.</summary>
    Task<CrossCuttingIssueScoreDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the score for one cross-cutting issue, then recalculates the weighted score for
    /// every issue and the overall combined score.
    /// </summary>
    /// <param name="id">Identifier of the issue being updated.</param>
    /// <param name="score">New score, from 1 to 5.</param>
    Task<CrossCuttingIssueRecalculationResultDto> UpdateScoreAsync(int id, int score, CancellationToken cancellationToken = default);
}
