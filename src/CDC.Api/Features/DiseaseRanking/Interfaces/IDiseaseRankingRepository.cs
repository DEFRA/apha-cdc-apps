namespace CDC.Api.Features.DiseaseRanking.Interfaces;

/// <summary>A saved disease ranking filter, selectable as a report.</summary>
/// <param name="Id">The filter identifier.</param>
/// <param name="Name">The filter name.</param>
public sealed record DiseaseRankingFilter(Guid Id, string Name);

/// <summary>
/// Data access for disease ranking reports. Mirrors the legacy
/// <c>DiseaseRankingService.GetDiseaseDropReferenceTables</c> and
/// <c>PrioritisationService.GetRankProfileDataForFilter</c>. Implementations contain no business logic.
/// </summary>
public interface IDiseaseRankingRepository
{
    /// <summary>Reads the saved filters via <c>spiGetDiseaseRankingFilterName</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The filters, in the order the procedure returns them.</returns>
    Task<IReadOnlyList<DiseaseRankingFilter>> GetFiltersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads every ranked profile via <c>spgaRankProfileData</c>, or those matching a saved filter
    /// via <c>spgaFilterRankProfileData</c>.
    /// </summary>
    /// <param name="filterName">The saved filter name, or <see langword="null"/> for every profile.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>One entry per profile and species, with a rank for each reason for intervention.</returns>
    Task<IReadOnlyList<Domain.Entities.RankedProfile>> GetRankedProfilesAsync(string? filterName, CancellationToken cancellationToken);
}
