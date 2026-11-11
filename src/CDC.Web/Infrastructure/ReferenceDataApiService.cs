using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Infrastructure;

/// <summary>
/// <see cref="IReferenceDataApiService"/> backed by CDC.Api's <c>/api/reference-data/maintainable-tables</c> endpoints, which call
/// the legacy reference data stored procedures. The base address is configured on the <see cref="HttpClient"/> in Program.cs.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ReferenceDataApiService(HttpClient httpClient) : IReferenceDataApiService
{
    private const string BasePath = "/api/reference-data/maintainable-tables";
    private const string DuplicateMessageFragment = "already a reference value";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceTableSummary>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await httpClient.GetFromJsonAsync<IReadOnlyList<TableDto>>(BasePath, cancellationToken);

        return [.. (tables ?? []).Select(table => new ReferenceTableSummary(table.Id, table.Name))];
    }

    /// <inheritdoc />
    public async Task<ReferenceTableSummary?> GetTableAsync(Guid referenceTableId, CancellationToken cancellationToken = default) =>
        (await GetTablesAsync(cancellationToken)).FirstOrDefault(table => table.Id == referenceTableId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceDataValue>> GetValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default)
    {
        var values = await GetOrDefaultAsync<IReadOnlyList<ValueDto>>($"{BasePath}/{referenceTableId}/values", cancellationToken);

        // The API returns the values in sequence order but not the number itself, which is only used for ordering.
        return [.. (values ?? []).Select((value, index) => value.ToModel(referenceTableId, (byte)Math.Min(index + 1, byte.MaxValue)))];
    }

    /// <inheritdoc />
    public async Task<ReferenceDataValue?> GetValueAsync(Guid referenceTableId, Guid valueId, CancellationToken cancellationToken = default)
    {
        var value = await GetOrDefaultAsync<ValueDto>($"{BasePath}/{referenceTableId}/values/{valueId}", cancellationToken);

        return value?.ToModel(referenceTableId, 0);
    }

    /// <inheritdoc />
    public async Task<ReferenceDataUpdateResult> UpdateValueAsync(ReferenceDataUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        // The acting user is added by ActingUserDelegatingHandler; the API records them, not update.UserFullName.
        using var response = await httpClient.PostAsJsonAsync(
            $"{BasePath}/{update.ReferenceTableId}/changes",
            new
            {
                Action = (int)update.Action,
                ValueId = update.Action == ReferenceDataChangeAction.Add ? (Guid?)null : update.ValueId,
                update.NewLookupValue,
                update.Reason,
                update.LastUpdated
            },
            cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.ValueNotFound, null);

            case HttpStatusCode.Conflict:
                return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.Conflict, null);

            case HttpStatusCode.BadRequest when await IsDuplicateAsync(response, cancellationToken):
                return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.DuplicateLookupValue, null);

            default:
                response.EnsureSuccessStatusCode();
                return new ReferenceDataUpdateResult(ReferenceDataUpdateOutcome.Updated, null);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceDataAuditEntry>> GetAuditTrailAsync(Guid referenceTableId, CancellationToken cancellationToken = default)
    {
        var entries = await GetOrDefaultAsync<IReadOnlyList<AuditEntryDto>>($"{BasePath}/{referenceTableId}/audit-trail", cancellationToken);

        return [.. (entries ?? []).Select(entry => new ReferenceDataAuditEntry(
            entry.Id,
            entry.ReferenceTableId,
            entry.TableName,
            entry.OldLookupValue,
            entry.NewLookupValue,
            entry.UserFullName,
            entry.EffectiveDate,
            entry.LogDate,
            entry.Reason))];
    }

    /// <inheritdoc />
    public async Task<ReferenceDataAuditEntry?> GetAuditEntryAsync(Guid referenceTableId, Guid auditId, CancellationToken cancellationToken = default) =>
        (await GetAuditTrailAsync(referenceTableId, cancellationToken)).FirstOrDefault(entry => entry.Id == auditId);

    private static async Task<bool> IsDuplicateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);

        return problem?.Detail?.Contains(DuplicateMessageFragment, StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<T?> GetOrDefaultAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private sealed record TableDto(Guid Id, string Name);

    private sealed record ValueDto(
        Guid Id,
        string LookupValue,
        DateTime? EffectiveDateFrom,
        DateTime? EffectiveDateTo,
        bool IsInUse,
        byte[]? LastUpdated)
    {
        public ReferenceDataValue ToModel(Guid referenceTableId, byte sequenceNumber) => new(
            Id,
            referenceTableId,
            LookupValue,
            EffectiveDateFrom is { } from ? DateOnly.FromDateTime(from) : null,
            EffectiveDateTo is { } to ? DateOnly.FromDateTime(to) : null,
            sequenceNumber,
            IsInUse,
            LastUpdated);
    }

    private sealed record AuditEntryDto(
        Guid Id,
        Guid ReferenceTableId,
        string TableName,
        string OldLookupValue,
        string NewLookupValue,
        string UserFullName,
        DateTime EffectiveDate,
        DateTime LogDate,
        string Reason);
}
