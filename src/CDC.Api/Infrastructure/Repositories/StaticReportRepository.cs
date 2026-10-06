using System.Data;
using System.Data.Common;
using System.Globalization;
using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IStaticReportRepository"/>.
/// </summary>
/// <remarks>
/// The list procedures return positional columns with no usable names, so they are read through a
/// data reader in the same column order as the legacy
/// <c>Profiles.DataAccess.Sql.StaticReportService.GetStaticReports</c>.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class StaticReportRepository(IDbConnectionFactory connectionFactory, ILogger<StaticReportRepository> logger)
    : IStaticReportRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportDto>> GetCurrentStaticReportsAsync(
        bool isUserManual,
        bool publicOnly,
        CancellationToken cancellationToken) =>
        await ReadStaticReportsAsync(
            StaticReportStoredProcedures.GetCurrentStaticReports,
            new { IsUserManual = isUserManual, PublicOnly = publicOnly },
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly,
        CancellationToken cancellationToken) =>
        await ReadStaticReportsAsync(
            StaticReportStoredProcedures.GetStaticReportHistory,
            new { StaticReportId = staticReportId, PublicOnly = publicOnly },
            cancellationToken);

    /// <inheritdoc />
    public async Task<StaticReportDataDto?> GetStaticReportDataAsync(
        Guid staticReportVersionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    StaticReportStoredProcedures.GetStaticReportVersionData,
                    new { StaticReportVersionId = staticReportVersionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.SingleRow);

            // Columns: PdfData, IsPublic, Title.
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                logger.StaticReportVersionNotFound(staticReportVersionId);
                return null;
            }

            return new StaticReportDataDto
            {
                PdfData = (byte[])reader.GetValue(0),
                Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetStaticReportVersionData);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StaticReportStoredProcedures.DeleteStaticReportVersion,
                new { StaticReportVersionId = staticReportVersionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            logger.DeletedStaticReportVersion(staticReportVersionId);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.DeleteStaticReportVersion);
            throw;
        }
    }

    private async Task<IReadOnlyList<StaticReportDto>> ReadStaticReportsAsync(
        string storedProcedure,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    storedProcedure,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var reports = new List<StaticReportDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                reports.Add(new StaticReportDto
                {
                    Id = reader.GetGuid(0),
                    StaticReportId = reader.GetGuid(1),
                    Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    VersionMajor = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    EffectiveDateFrom = reader.GetDateTime(4),
                    EffectiveDateTo = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    IsUserManual = !reader.IsDBNull(6) && reader.GetBoolean(6),
                    IsPublic = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    FileSize = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture)
                });
            }

            return reports;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);
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
                $"{nameof(StaticReportRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }
}
