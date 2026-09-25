using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the species endpoints on CDC.Api.
/// </summary>
public interface ISpeciesApiService
{
    /// <summary>Calls <c>GET /api/species</c> and returns every species and species group.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The full species list, in the order returned by the API.</returns>
    Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/{speciesId}/detail</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The species detail, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/{speciesId}/valid-parents</c>.</summary>
    /// <param name="speciesId">The species being re-parented.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The valid parent list.</returns>
    Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/species/name-parent</c>.</summary>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<UpdateSpeciesNameParentResult> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/audit-trail</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The audit trail, most recent entry first.</returns>
    Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken = default);
}
