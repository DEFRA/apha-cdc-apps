using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>Dapper implementation of <see cref="IReferenceDataMaintenanceRepository"/>.</summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReferenceDataMaintenanceRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ReferenceDataMaintenanceRepository> logger)
    : IReferenceDataMaintenanceRepository
{
    /// <summary>The procedures declare <c>varchar(255)</c> for both the lookup value and the reason.</summary>
    private const int TextMaxLength = 255;

    private const int RowVersionLength = 8;

    // The procedures RAISERROR these texts; matched on the message because they share error number 50000.
    // "not maintainable" (spgReferenceValueByTable) and "a maintainable reference table" (spgReferenceValue) share this word.
    private const string NotMaintainableMessageFragment = "maintainable";
    private const string ConcurrencyMessageFragment = "edited by another user";
    private const string DuplicateMessageFragment = "already a reference value with this name";

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainableReferenceTableDto>> GetMaintainableTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<TableRow>(new CommandDefinition(
                ReferenceDataStoredProcedures.GetMaintainableTables,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new MaintainableReferenceTableDto { Id = row.Id, Name = row.Name ?? string.Empty })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ReferenceDataStoredProcedures.GetMaintainableTables);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainableReferenceValueDto>?> GetMaintainableValuesAsync(
        Guid referenceTableId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ValueRow>(new CommandDefinition(
                ReferenceDataStoredProcedures.GetMaintainableValuesByTable,
                new { ReferenceTableId = referenceTableId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => row.ToDto())];
        }
        catch (DbException exception) when (IsNotMaintainable(exception))
        {
            return null;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ReferenceDataStoredProcedures.GetMaintainableValuesByTable);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetValueRowVersionAsync(Guid valueId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<ValueRow>(new CommandDefinition(
                ReferenceDataStoredProcedures.GetMaintainableValue,
                new { ValueId = valueId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return row?.LastUpdated;
        }
        catch (DbException exception) when (IsNotMaintainable(exception))
        {
            return null;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ReferenceDataStoredProcedures.GetMaintainableValue);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceTableAuditEntryDto>> GetAuditTrailAsync(
        Guid referenceTableId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(
                ReferenceDataStoredProcedures.GetAuditTrail,
                new { ReferenceTableId = referenceTableId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ReferenceTableAuditEntryDto
            {
                Id = row.Id,
                ReferenceTableId = referenceTableId,
                OldLookupValue = row.OldLookupValue ?? string.Empty,
                NewLookupValue = row.NewLookupValue ?? string.Empty,
                UserFullName = row.FullName ?? string.Empty,
                EffectiveDate = row.EffectiveDate,
                LogDate = row.LogDate,
                Reason = row.Reason ?? string.Empty
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ReferenceDataStoredProcedures.GetAuditTrail);
            throw;
        }
    }

    /// <inheritdoc />
    public Task UpdateValueAsync(
        Guid valueId,
        string newLookupValue,
        Guid userId,
        string reason,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ValueId", valueId, DbType.Guid);
        parameters.Add("@NewLookupValue", newLookupValue, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@UserId", userId, DbType.Guid);
        parameters.Add("@Reason", reason, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);

        return ExecuteChangeAsync(ReferenceDataStoredProcedures.UpdateValue, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task InsertValueAsync(
        Guid referenceTableId,
        Guid valueId,
        string newLookupValue,
        Guid userId,
        string reason,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ReferenceTableId", referenceTableId, DbType.Guid);
        parameters.Add("@ValueId", valueId, DbType.Guid);
        parameters.Add("@NewLookupValue", newLookupValue, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@UserId", userId, DbType.Guid);
        parameters.Add("@Reason", reason, DbType.AnsiString, size: TextMaxLength);

        return ExecuteChangeAsync(ReferenceDataStoredProcedures.InsertValue, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task InactivateValueAsync(
        Guid valueId,
        Guid userId,
        string reason,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ValueId", valueId, DbType.Guid);
        parameters.Add("@UserId", userId, DbType.Guid);
        parameters.Add("@Reason", reason, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);

        return ExecuteChangeAsync(ReferenceDataStoredProcedures.InactivateValue, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteValueAsync(
        Guid valueId,
        string oldLookupValue,
        Guid userId,
        string reason,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ValueId", valueId, DbType.Guid);
        parameters.Add("@OldLookupValue", oldLookupValue, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@UserId", userId, DbType.Guid);
        parameters.Add("@Reason", reason, DbType.AnsiString, size: TextMaxLength);
        parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);

        return ExecuteChangeAsync(ReferenceDataStoredProcedures.DeleteValue, parameters, cancellationToken);
    }

    private static bool IsNotMaintainable(DbException exception) =>
        exception.Message.Contains(NotMaintainableMessageFragment, StringComparison.OrdinalIgnoreCase);

    private async Task ExecuteChangeAsync(string storedProcedure, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                storedProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);

            // The procedures' own messages already say what went wrong, so they are passed through unchanged.
            if (exception.Message.Contains(ConcurrencyMessageFragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConcurrencyException(exception.Message, exception);
            }

            if (exception.Message.Contains(DuplicateMessageFragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new DuplicateReferenceValueException(DuplicateReferenceValueException.DefaultMessage, exception);
            }

            throw;
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ReferenceDataMaintenanceRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        catch
        {
            await dbConnection.DisposeAsync();
            throw;
        }

        return dbConnection;
    }

    private sealed record TableRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? Name { get; init; } // NOSONAR
    }

    private sealed record ValueRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? LookupValue { get; init; } // NOSONAR
        public DateTime? EffectiveDateFrom { get; init; } // NOSONAR
        public DateTime? EffectiveDateTo { get; init; } // NOSONAR
        public bool IsInUse { get; init; } // NOSONAR
        public bool IsActive { get; init; } // NOSONAR
        public byte[]? LastUpdated { get; init; } // NOSONAR

        public MaintainableReferenceValueDto ToDto() => new()
        {
            Id = Id,
            LookupValue = LookupValue ?? string.Empty,
            EffectiveDateFrom = EffectiveDateFrom,
            EffectiveDateTo = EffectiveDateTo,
            IsInUse = IsInUse,
            IsActive = IsActive
        };
    }

    private sealed record AuditRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? OldLookupValue { get; init; } // NOSONAR
        public string? NewLookupValue { get; init; } // NOSONAR
        public string? FullName { get; init; } // NOSONAR
        public DateTime EffectiveDate { get; init; } // NOSONAR
        public DateTime LogDate { get; init; } // NOSONAR
        public string? Reason { get; init; } // NOSONAR
    }
}
