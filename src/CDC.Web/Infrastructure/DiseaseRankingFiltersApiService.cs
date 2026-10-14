using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IDiseaseRankingFiltersApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>).
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class DiseaseRankingFiltersApiService(HttpClient httpClient) : IDiseaseRankingFiltersApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DiseaseRankingFilterSummaryDto>> GetFiltersAsync(CancellationToken cancellationToken = default)
    {
        var filters = await httpClient.GetFromJsonAsync<IReadOnlyList<DiseaseRankingFilterSummaryDto>>(
            "/api/disease-ranking-filters", cancellationToken);

        return filters ?? [];
    }

    /// <inheritdoc />
    public async Task<DiseaseRankingFilterDetailDto?> GetFilterDetailAsync(long filterNumber, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/disease-ranking-filters/{filterNumber}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DiseaseRankingFilterDetailDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SaveDiseaseRankingFilterResult> CreateFilterAsync(
        SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/disease-ranking-filters", request, cancellationToken);

        return await ReadSaveResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SaveDiseaseRankingFilterResult> UpdateFilterAsync(
        long filterNumber,
        SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"/api/disease-ranking-filters/{filterNumber}", request, cancellationToken);

        return await ReadSaveResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteFilterAsync(long filterNumber, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/api/disease-ranking-filters/{filterNumber}", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<SaveDiseaseRankingFilterResult> ReadSaveResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var saved = await response.Content.ReadFromJsonAsync<DiseaseRankingFilterDetailDto>(cancellationToken);

            return new SaveDiseaseRankingFilterResult
            {
                Outcome = DiseaseRankingFilterSaveOutcome.Success,
                FilterNumber = saved?.FilterNumber ?? 0
            };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new SaveDiseaseRankingFilterResult
            {
                Outcome = DiseaseRankingFilterSaveOutcome.DuplicateName,
                ErrorMessage = "Filter name already exists"
            },
            HttpStatusCode.BadRequest => new SaveDiseaseRankingFilterResult
            {
                Outcome = DiseaseRankingFilterSaveOutcome.ValidationFailed,
                ErrorMessage = "Please select the species from the list"
            },
            _ => new SaveDiseaseRankingFilterResult
            {
                Outcome = DiseaseRankingFilterSaveOutcome.Error,
                ErrorMessage = "We could not save this filter. Try again later."
            }
        };
    }
}
