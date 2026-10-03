using CDC.Api.Features.ReferenceData.Dtos;

namespace CDC.Api.Features.ReferenceData.Interfaces;

/// <summary>
/// Application service for the reference data feature.
/// </summary>
public interface IReferenceDataService
{
    /// <summary>Gets every value in one reference table.</summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The reference values.</returns>
    Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken);
}
