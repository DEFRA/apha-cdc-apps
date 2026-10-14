using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the disease ranking filter endpoints on CDC.Api.
/// </summary>
public interface IDiseaseRankingFiltersApiService
{
    /// <summary>Calls <c>GET /api/disease-ranking-filters</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active filter, for the "Select Filter" dropdown.</returns>
    Task<IReadOnlyList<DiseaseRankingFilterSummaryDto>> GetFiltersAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/disease-ranking-filters/{filterNumber}</c>.</summary>
    /// <param name="filterNumber">The filter to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The filter detail, or <see langword="null"/> when it does not exist.</returns>
    Task<DiseaseRankingFilterDetailDto?> GetFilterDetailAsync(long filterNumber, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/disease-ranking-filters</c>.</summary>
    /// <param name="request">The new filter's name, criteria and selected species.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call, including the new filter number on success.</returns>
    Task<SaveDiseaseRankingFilterResult> CreateFilterAsync(SaveDiseaseRankingFilterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/disease-ranking-filters/{filterNumber}</c>.</summary>
    /// <param name="filterNumber">The filter to update.</param>
    /// <param name="request">The filter's new name, criteria and selected species.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<SaveDiseaseRankingFilterResult> UpdateFilterAsync(
        long filterNumber,
        SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/disease-ranking-filters/{filterNumber}</c>.</summary>
    /// <param name="filterNumber">The filter to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task DeleteFilterAsync(long filterNumber, CancellationToken cancellationToken = default);
}
