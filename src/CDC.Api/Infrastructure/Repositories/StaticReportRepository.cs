using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IStaticReportRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class StaticReportRepository(IDbConnectionFactory connectionFactory, ILogger<StaticReportRepository> logger)
    : IStaticReportRepository
{
    /// <summary><c>spgaCurrentStaticReport</c> rejects <c>PublicOnly</c> for user manuals, so this is always "no".</summary>
    private const bool PublicOnly = false;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersion>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            // spgaCurrentStaticReport's last column is an unaliased DATALENGTH(...) expression,
            // so Dapper's name-based constructor matching can't bind it - read by ordinal instead.
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                StaticReportStoredProcedures.GetCurrent,
                new { IsUserManual = isUserManual, PublicOnly },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var versions = new List<StaticReportVersion>();

            while (await reader.ReadAsync(cancellationToken))
            {
                versions.Add(new StaticReportVersion
                {
                    Id = reader.GetGuid(0),
                    StaticReportId = reader.GetGuid(1),
                    Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    VersionMajor = reader.GetByte(3),
                    EffectiveDateFrom = reader.GetDateTime(4),
                    EffectiveDateTo = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    IsUserManual = reader.GetBoolean(6),
                    IsPublic = reader.GetBoolean(7),
                    FileSize = reader.GetInt32(8)
                });
            }

            logger.RetrievedCurrentReports(versions.Count, isUserManual);

            return versions;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetCurrent);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StaticReportData?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<StaticReportDataRow>(new CommandDefinition(
                StaticReportStoredProcedures.GetData,
                new { StaticReportVersionId = staticReportVersionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            if (row is null)
            {
                logger.ReportDataNotFound(staticReportVersionId);
                return null;
            }

            logger.RetrievedReportData(staticReportVersionId);

            return new StaticReportData
            {
                PdfData = row.PdfData,
                IsPublic = row.IsPublic,
                Title = row.Title
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetData);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UploadAsync(string title, byte[] pdfData, bool isUserManual, bool isPublic, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StaticReportStoredProcedures.Upload,
                new { Title = title, PdfData = pdfData, IsUserManual = isUserManual, IsPublic = isPublic },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            logger.UploadedReport(title, isUserManual);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.Upload);
            throw;
        }
    }

    /// <summary>Shape of the row returned by <c>spgStaticReportVersionData</c>.</summary>
    private sealed record StaticReportDataRow(byte[] PdfData, bool IsPublic, string Title);

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
