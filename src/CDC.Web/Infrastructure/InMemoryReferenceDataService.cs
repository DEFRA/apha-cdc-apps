using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// In-memory <see cref="IReferenceDataService"/> used until the reference data endpoints exist on
/// CDC.Api. Seeded from the legacy Profiles baseline data and follows the same update rules as
/// spuReferenceValue: only the lookup value changes, the effective date is stamped by the system,
/// duplicate values within a table are rejected, and an audit entry is written for every change.
/// Registered as a singleton so edits survive between requests within a single container; it is
/// deliberately not durable.
/// </summary>
/// <param name="timeProvider">Supplies the effective date and log date written to the audit trail.</param>
public sealed class InMemoryReferenceDataService(TimeProvider timeProvider) : IReferenceDataService
{
    private readonly Lock _gate = new();
    private readonly List<ReferenceDataValue> _values = [.. ReferenceDataSeed.Values];
    private readonly List<ReferenceDataAuditEntry> _auditTrail = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceTableSummary>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ReferenceDataSeed.Tables);

    /// <inheritdoc />
    public Task<ReferenceTableSummary?> GetTableAsync(Guid referenceTableId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FindTable(referenceTableId));

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceDataValue>> GetValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ReferenceDataValue> values =
            [
                .. _values
                    .Where(value => value.ReferenceTableId == referenceTableId)
                    .OrderBy(value => value.SequenceNumber)
                    .ThenBy(value => value.EffectiveDateFrom)
            ];

            return Task.FromResult(values);
        }
    }

    /// <inheritdoc />
    public Task<ReferenceDataValue?> GetValueAsync(Guid referenceTableId, Guid valueId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_values.FirstOrDefault(value =>
                value.Id == valueId && value.ReferenceTableId == referenceTableId));
        }
    }

    /// <inheritdoc />
    public Task<ReferenceDataUpdateResult> UpdateValueAsync(ReferenceDataUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        // A reason for change is what makes the audit trail meaningful, so it is enforced here as
        // well as in the page model - no code path may write a change without one.
        if (string.IsNullOrWhiteSpace(update.Reason))
        {
            throw new ArgumentException("A reason for change is required.", nameof(update));
        }

        var table = FindTable(update.ReferenceTableId);

        if (table is null)
        {
            return Task.FromResult(new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueNotFound, null));
        }

        lock (_gate)
        {
            var index = _values.FindIndex(value =>
                value.Id == update.ValueId && value.ReferenceTableId == table.Id);

            if (index < 0)
            {
                return Task.FromResult(new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueNotFound, null));
            }

            if (_values.Exists(value =>
                    value.ReferenceTableId == table.Id &&
                    value.Id != update.ValueId &&
                    string.Equals(value.LookupValue, update.NewLookupValue, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.DuplicateLookupValue, null));
            }

            var existing = _values[index];
            var changedOn = timeProvider.GetUtcNow();

            var audit = new ReferenceDataAuditEntry(
                Guid.NewGuid(),
                table.Id,
                table.Name,
                existing.Id,
                existing.LookupValue,
                update.NewLookupValue,
                update.UserFullName,
                changedOn,
                changedOn,
                update.Reason);

            _values[index] = existing with
            {
                LookupValue = update.NewLookupValue,
                EffectiveDateFrom = DateOnly.FromDateTime(changedOn.UtcDateTime)
            };
            _auditTrail.Add(audit);

            return Task.FromResult(new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.Updated, audit));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReferenceDataAuditEntry>> GetAuditTrailAsync(Guid? referenceTableId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ReferenceDataAuditEntry> entries =
            [
                .. _auditTrail
                    .Where(entry => referenceTableId is null || entry.ReferenceTableId == referenceTableId)
                    .OrderByDescending(entry => entry.LogDate)
            ];

            return Task.FromResult(entries);
        }
    }

    /// <inheritdoc />
    public Task<ReferenceDataAuditEntry?> GetAuditEntryAsync(Guid auditId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_auditTrail.FirstOrDefault(entry => entry.Id == auditId));
        }
    }

    private static ReferenceTableSummary? FindTable(Guid referenceTableId) =>
        ReferenceDataSeed.Tables.FirstOrDefault(table => table.Id == referenceTableId);
}
