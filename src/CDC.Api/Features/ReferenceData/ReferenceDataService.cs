using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.ReferenceData.Mapping;

namespace CDC.Api.Features.ReferenceData;

/// <summary>
/// Default <see cref="IReferenceDataService"/>: reads through <see cref="IReferenceDataRepository"/>
/// and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Reference data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReferenceDataService(IReferenceDataRepository repository, ILogger<ReferenceDataService> logger)
    : IReferenceDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken)
    {
        var values = await repository.GetReferenceValuesAsync(referenceTableId, cancellationToken);
        logger.RetrievedReferenceValues(values.Count, referenceTableId);

        return [.. values.Select(value => value.ToDto())];
    }
}
