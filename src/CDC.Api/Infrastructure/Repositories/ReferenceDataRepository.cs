using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IReferenceDataRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReferenceDataRepository(IDbConnectionFactory connectionFactory, ILogger<ReferenceDataRepository> logger)
    : IReferenceDataRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceValue>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ReferenceValueRow>(new CommandDefinition(
                ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable,
                new { ReferenceTableId = referenceTableId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ReferenceValue
            {
                Id = row.Id,
                Value = row.LookupValue ?? string.Empty
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable);
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
                $"{nameof(ReferenceDataRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    private sealed record ReferenceValueRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? LookupValue { get; init; } // NOSONAR
    }
}
