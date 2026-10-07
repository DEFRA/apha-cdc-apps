using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Reads and maintains application reference data, recording an audit entry for every change.
/// Mirrors the legacy Profiles procedures spgMaintainableReferenceTables, spgReferenceValueByTable,
/// spuReferenceValue and spgReferenceTableAudit.
/// </summary>
public interface IReferenceDataService
{
    /// <summary>Gets the maintainable reference tables offered in the dropdown.</summary>
    Task<IReadOnlyList<ReferenceTableSummary>> GetTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the maintainable table with the supplied id, or <see langword="null"/> if there is none.</summary>
    Task<ReferenceTableSummary?> GetTableAsync(Guid referenceTableId, CancellationToken cancellationToken = default);

    /// <summary>Gets the values held in the supplied table, in sequence number order.</summary>
    Task<IReadOnlyList<ReferenceDataValue>> GetValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single value, or <see langword="null"/> if it does not exist in that table.</summary>
    Task<ReferenceDataValue?> GetValueAsync(Guid referenceTableId, Guid valueId, CancellationToken cancellationToken = default);

    /// <summary>Applies the change and writes the matching audit entry.</summary>
    Task<ReferenceDataUpdateResult> UpdateValueAsync(ReferenceDataUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the audit trail, most recent change first. Pass a table id to filter to one table,
    /// or <see langword="null"/> for every table.
    /// </summary>
    Task<IReadOnlyList<ReferenceDataAuditEntry>> GetAuditTrailAsync(Guid? referenceTableId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single audit entry, or <see langword="null"/> if it does not exist.</summary>
    Task<ReferenceDataAuditEntry?> GetAuditEntryAsync(Guid auditId, CancellationToken cancellationToken = default);
}
