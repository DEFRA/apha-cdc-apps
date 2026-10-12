using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.DiseaseRankingFilters.Interfaces;

/// <summary>
/// Data access for disease ranking filters. Implementations must contain no business logic.
/// </summary>
public interface IDiseaseRankingFiltersRepository
{
    /// <summary>Reads every active filter via <c>spgDiseaseRankingFilters</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every active filter, ordered by name.</returns>
    Task<IReadOnlyList<DiseaseRankingFilterSummary>> GetFiltersAsync(CancellationToken cancellationToken);

    /// <summary>Reads one filter's detail via <c>spgDiseaseRankingFilterDetail</c>.</summary>
    /// <param name="filterNumber">The filter to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The filter detail, or <see langword="null"/> when no active rows exist for that filter number.</returns>
    Task<DiseaseRankingFilterDetail?> GetFilterDetailAsync(long filterNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Reserves the next filter number for a new filter name via <c>spiGetMaxFilterNumber</c>.
    /// </summary>
    /// <param name="diseaseName">The proposed filter name.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new filter number, or 0 when an active filter already has this name.</returns>
    Task<long> ReserveFilterNumberAsync(string diseaseName, CancellationToken cancellationToken);

    /// <summary>Inserts one species row for a filter via <c>spiDiseaseFilterValue</c>.</summary>
    /// <param name="filterNumber">The filter the species belongs to.</param>
    /// <param name="request">The filter's name, criteria and the species being inserted.</param>
    /// <param name="speciesId">The species to record this row for.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task InsertFilterSpeciesAsync(
        long filterNumber,
        Dtos.SaveDiseaseRankingFilterRequestDto request,
        Guid speciesId,
        CancellationToken cancellationToken);

    /// <summary>Updates (or re-inserts) one species row for a filter via <c>spiUpdateDiseaseFilterValue</c>.</summary>
    /// <param name="filterNumber">The filter the species belongs to.</param>
    /// <param name="request">The filter's new name, criteria and the species being updated.</param>
    /// <param name="speciesId">The species to update this row for.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateFilterSpeciesAsync(
        long filterNumber,
        Dtos.SaveDiseaseRankingFilterRequestDto request,
        Guid speciesId,
        CancellationToken cancellationToken);

    /// <summary>Soft-deletes every row for a filter via <c>spiUpdateDisableDiseaseFilterValue</c>.</summary>
    /// <param name="filterNumber">The filter to delete.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task DeleteFilterAsync(long filterNumber, CancellationToken cancellationToken);
}
