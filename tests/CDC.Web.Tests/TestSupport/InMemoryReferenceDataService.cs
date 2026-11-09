using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="IReferenceDataApiService"/> that follows the legacy reference value procedures' rules: duplicate
/// values within a table are rejected, inactive values cannot be changed, values in use cannot be deleted, a stale row version
/// is a conflict, and an audit entry is written for every change.
/// </summary>
/// <param name="timeProvider">Supplies the effective date and log date written to the audit trail.</param>
public sealed class InMemoryReferenceDataService(TimeProvider timeProvider) : IReferenceDataApiService
{
    private readonly Lock _gate = new();
    private readonly List<ReferenceDataValue> _values = [.. ReferenceDataSeed.Values];
    private readonly List<ReferenceDataAuditEntry> _auditTrail = [];
    private long _rowVersion = 1;

    public Task<IReadOnlyList<ReferenceTableSummary>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ReferenceDataSeed.Tables);

    public Task<ReferenceTableSummary?> GetTableAsync(Guid referenceTableId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FindTable(referenceTableId));

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

    public Task<ReferenceDataValue?> GetValueAsync(Guid referenceTableId, Guid valueId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_values.FirstOrDefault(value =>
                value.Id == valueId && value.ReferenceTableId == referenceTableId));
        }
    }

    public Task<ReferenceDataUpdateResult> UpdateValueAsync(ReferenceDataUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var table = FindTable(update.ReferenceTableId);

        if (table is null)
        {
            return Task.FromResult(new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueNotFound, null));
        }

        lock (_gate)
        {
            var changedOn = timeProvider.GetUtcNow().UtcDateTime;

            return Task.FromResult(update.Action == ReferenceDataChangeAction.Add
                ? AddValue(table, update, changedOn)
                : ChangeValue(table, update, changedOn));
        }
    }

    public Task<IReadOnlyList<ReferenceDataAuditEntry>> GetAuditTrailAsync(Guid referenceTableId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ReferenceDataAuditEntry> entries =
            [
                .. _auditTrail
                    .Where(entry => entry.ReferenceTableId == referenceTableId)
                    .OrderByDescending(entry => entry.LogDate)
            ];

            return Task.FromResult(entries);
        }
    }

    public Task<ReferenceDataAuditEntry?> GetAuditEntryAsync(Guid referenceTableId, Guid auditId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_auditTrail.FirstOrDefault(entry => entry.Id == auditId && entry.ReferenceTableId == referenceTableId));
        }
    }

    private static ReferenceTableSummary? FindTable(Guid referenceTableId) =>
        ReferenceDataSeed.Tables.FirstOrDefault(table => table.Id == referenceTableId);

    private byte[] NextRowVersion() => BitConverter.GetBytes(Interlocked.Increment(ref _rowVersion));

    private ReferenceDataUpdateResult AddValue(ReferenceTableSummary table, ReferenceDataUpdate update, DateTime changedOn)
    {
        if (IsDuplicate(table.Id, update.NewLookupValue))
        {
            return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.DuplicateLookupValue, null);
        }

        var nextSequenceNumber = _values
            .Where(value => value.ReferenceTableId == table.Id)
            .Select(value => (int)value.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var added = new ReferenceDataValue(
            Guid.NewGuid(),
            table.Id,
            update.NewLookupValue,
            DateOnly.FromDateTime(changedOn),
            EffectiveDateTo: null,
            (byte)Math.Min(nextSequenceNumber, byte.MaxValue),
            IsInUse: false,
            NextRowVersion());

        _values.Add(added);

        return Record(table, ReferenceDataPlaceholders.NewEntry, update.NewLookupValue, update, changedOn);
    }

    private ReferenceDataUpdateResult ChangeValue(ReferenceTableSummary table, ReferenceDataUpdate update, DateTime changedOn)
    {
        var index = _values.FindIndex(value => value.Id == update.ValueId && value.ReferenceTableId == table.Id);

        if (index < 0)
        {
            return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueNotFound, null);
        }

        var existing = _values[index];

        if (!existing.IsActive)
        {
            return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueInactive, null);
        }

        if (update.LastUpdated is not null && existing.LastUpdated is not null && !update.LastUpdated.SequenceEqual(existing.LastUpdated))
        {
            return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.Conflict, null);
        }

        switch (update.Action)
        {
            case ReferenceDataChangeAction.Inactivate:
                _values[index] = existing with { EffectiveDateTo = DateOnly.FromDateTime(changedOn), LastUpdated = NextRowVersion() };
                return Record(table, existing.LookupValue, ReferenceDataPlaceholders.ToBeInactivated, update, changedOn);

            case ReferenceDataChangeAction.Delete:
                if (existing.IsInUse)
                {
                    return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueInUse, null);
                }

                _values.RemoveAt(index);
                return Record(table, existing.LookupValue, ReferenceDataPlaceholders.ToBeDeleted, update, changedOn);

            default:
                if (IsDuplicate(table.Id, update.NewLookupValue))
                {
                    return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.DuplicateLookupValue, null);
                }

                _values[index] = existing with { LookupValue = update.NewLookupValue, LastUpdated = NextRowVersion() };
                return Record(table, existing.LookupValue, update.NewLookupValue, update, changedOn);
        }
    }

    // As spuReferenceValue, the value being changed counts too, so "changing" it to the same text is a duplicate.
    private bool IsDuplicate(Guid referenceTableId, string lookupValue) =>
        _values.Exists(value =>
            value.ReferenceTableId == referenceTableId &&
            string.Equals(value.LookupValue, lookupValue, StringComparison.OrdinalIgnoreCase));

    private ReferenceDataUpdateResult Record(
        ReferenceTableSummary table,
        string oldLookupValue,
        string newLookupValue,
        ReferenceDataUpdate update,
        DateTime changedOn)
    {
        var audit = new ReferenceDataAuditEntry(
            Guid.NewGuid(),
            table.Id,
            table.Name,
            oldLookupValue,
            newLookupValue,
            update.UserFullName,
            changedOn,
            changedOn,
            update.Reason);

        _auditTrail.Add(audit);

        return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.Updated, audit);
    }
}
