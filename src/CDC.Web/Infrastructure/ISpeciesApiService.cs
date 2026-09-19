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
}
