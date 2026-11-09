using CDC.Api.Features.ReferenceData.Dtos;

namespace CDC.Api.Features.ReferenceData.Interfaces;

/// <summary>
/// Data access for the "maintain reference data" admin screen, through the stored procedures the legacy
/// <c>ReferenceValueService</c> used. The change procedures write the audit entry themselves.
/// </summary>
public interface IReferenceDataMaintenanceRepository
{
    /// <summary>Reads the maintainable reference tables via <c>spgMaintainableReferenceTables</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The tables, in the order the procedure returns them.</returns>
    Task<IReadOnlyList<MaintainableReferenceTableDto>> GetMaintainableTablesAsync(CancellationToken cancellationToken);

    /// <summary>Reads one maintainable table's values via <c>spgReferenceValueByTable</c>.</summary>
    /// <param name="referenceTableId">The table to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The values in sequence order, or <see langword="null"/> when the table does not exist or is not maintainable.</returns>
    Task<IReadOnlyList<MaintainableReferenceValueDto>?> GetMaintainableValuesAsync(Guid referenceTableId, CancellationToken cancellationToken);

    /// <summary>Reads one value's row version via <c>spgReferenceValue</c>.</summary>
    /// <param name="valueId">The value to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The row version, or <see langword="null"/> when the value does not exist or is not maintainable.</returns>
    Task<byte[]?> GetValueRowVersionAsync(Guid valueId, CancellationToken cancellationToken);

    /// <summary>Reads one table's audit trail via <c>spgReferenceTableAudit</c>, most recent change first.</summary>
    /// <param name="referenceTableId">The table to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The audit entries, without the table name.</returns>
    Task<IReadOnlyList<ReferenceTableAuditEntryDto>> GetAuditTrailAsync(Guid referenceTableId, CancellationToken cancellationToken);

    /// <summary>Changes a value's lookup text via <c>spuReferenceValue</c>.</summary>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">The value changed since <paramref name="lastUpdated"/> was read.</exception>
    Task UpdateValueAsync(Guid valueId, string newLookupValue, Guid userId, string reason, byte[] lastUpdated, CancellationToken cancellationToken);

    /// <summary>Adds a value via <c>spiReferenceValue</c>.</summary>
    /// <exception cref="Domain.Exceptions.DuplicateReferenceValueException">The table already has this lookup text.</exception>
    Task InsertValueAsync(Guid referenceTableId, Guid valueId, string newLookupValue, Guid userId, string reason, CancellationToken cancellationToken);

    /// <summary>End-dates a value via <c>sppReferenceValue</c>.</summary>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">The value changed since <paramref name="lastUpdated"/> was read.</exception>
    Task InactivateValueAsync(Guid valueId, Guid userId, string reason, byte[] lastUpdated, CancellationToken cancellationToken);

    /// <summary>Deletes a value via <c>spdReferenceValue</c>.</summary>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">The value changed since <paramref name="lastUpdated"/> was read.</exception>
    Task DeleteValueAsync(Guid valueId, string oldLookupValue, Guid userId, string reason, byte[] lastUpdated, CancellationToken cancellationToken);
}
