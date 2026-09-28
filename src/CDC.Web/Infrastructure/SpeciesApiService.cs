using System.Net;
using System.Net.Http.Json;
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

    /// <inheritdoc />
    public async Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/species/{speciesId}/detail", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SpeciesDetailDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(
        Guid speciesId,
        CancellationToken cancellationToken = default)
    {
        var validParents = await httpClient.GetFromJsonAsync<IReadOnlyList<SpeciesValidParentDto>>(
            $"/api/species/{speciesId}/valid-parents", cancellationToken);

        return validParents ?? [];
    }

    /// <inheritdoc />
    public async Task<UpdateSpeciesNameParentResult> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync("/api/species/name-parent", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateSpeciesNameParentResult { Outcome = SpeciesUpdateOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new UpdateSpeciesNameParentResult
            {
                Outcome = SpeciesUpdateOutcome.Conflict,
                ErrorMessage = "Another user has changed this species since it was opened. Reload and try again."
            },
            HttpStatusCode.BadRequest => new UpdateSpeciesNameParentResult
            {
                Outcome = SpeciesUpdateOutcome.ValidationFailed,
                ErrorMessage = "Enter a name, parent and reason for change."
            },
            _ => new UpdateSpeciesNameParentResult
            {
                Outcome = SpeciesUpdateOutcome.Error,
                ErrorMessage = "We could not save this change. Try again later."
            }
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken = default)
    {
        var entries = await httpClient.GetFromJsonAsync<IReadOnlyList<SpeciesAuditTrailEntryDto>>(
            "/api/species/audit-trail", cancellationToken);

        return entries ?? [];
    }
}
