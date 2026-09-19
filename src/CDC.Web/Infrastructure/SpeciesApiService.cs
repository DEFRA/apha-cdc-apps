using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="ISpeciesApiService"/>. The base address is configured on the underlying
/// <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is hardcoded here.
/// Failures are left to propagate - the caller (<c>ViewSpeciesDataModel</c>) decides how to
/// present them, matching the pattern used by <see cref="ApiConnectivityHealthCheck"/> for
/// <see cref="IApiClient"/>.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class SpeciesApiService(HttpClient httpClient) : ISpeciesApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken = default)
    {
        var species = await httpClient.GetFromJsonAsync<IReadOnlyList<SpeciesDto>>("/api/species", cancellationToken);

        return species ?? [];
    }
}
