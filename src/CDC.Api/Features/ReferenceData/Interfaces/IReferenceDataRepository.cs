using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ReferenceData.Interfaces;

/// <summary>
/// Data access for generic reference tables, used to resolve "List" type question field options.
/// </summary>
public interface IReferenceDataRepository
{
    /// <summary>Reads every value in one reference table via <c>spgReferenceValueByTable</c>.</summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The reference values, in the order the stored procedure returns them.</returns>
    Task<IReadOnlyList<ReferenceValue>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken);
}
