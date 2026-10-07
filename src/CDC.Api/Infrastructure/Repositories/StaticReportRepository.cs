using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IStaticReportRepository"/>.
/// </summary>
/// <remarks>
/// <c>spgaCurrentStaticReport</c> and <c>spgStaticReportHistory</c> return a trailing
/// <c>DATALENGTH(...)</c> column with no alias, so their result sets are read positionally
/// through a data reader exactly as <see cref="SpeciesRepository"/> does for its own
/// unnamed/duplicate columns, rather than relying on Dapper's by-name mapping.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class StaticReportRepository(IDbConnectionFactory connectionFactory, ILogger<StaticReportRepository> logger)
    : IStaticReportRepository
{
    /// <summary>SQL Server reports <c>RAISERROR</c> with a user-defined message as error 50000.</summary>
    private const int UserRaisedErrorNumber = 50000;

    /// <summary><c>spgaCurrentStaticReport</c> rejects <c>PublicOnly</c> for user manuals, so this is always "no".</summary>
    private const bool PublicOnly = false;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersion>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    StaticReportStoredProcedures.GetCurrent,
                    new { IsUserManual = isUserManual, PublicOnly },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var versions = await ReadVersionsAsync(reader, cancellationToken);

            logger.RetrievedCurrentStaticReports(versions.Count, isUserManual);

            return versions;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetCurrent);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersion>> GetHistoryAsync(Guid staticReportId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    StaticReportStoredProcedures.GetHistory,
                    new { StaticReportId = staticReportId, PublicOnly },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var versions = await ReadVersionsAsync(reader, cancellationToken);

            logger.RetrievedStaticReportHistory(versions.Count, staticReportId);

            return versions;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetHistory);
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
                logger.StaticReportDataNotFound(staticReportVersionId);
                return null;
            }

            logger.RetrievedStaticReportData(staticReportVersionId);

            return new StaticReportData
            {
                PdfData = row.PdfData ?? [],
                IsPublic = row.IsPublic,
                Title = row.Title ?? string.Empty
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.GetData);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UploadAsync(
        string title,
        byte[] pdfData,
        bool isUserManual,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StaticReportStoredProcedures.Upload,
                new { Title = title, PdfData = pdfData, IsUserManual = isUserManual, IsPublic = isPublic },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            logger.UploadedStaticReport(title, isUserManual, isPublic);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.Upload);

            throw IsUserRaisedError(exception)
                ? new ConcurrencyException(exception.Message, exception)
                : exception;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StaticReportStoredProcedures.Delete,
                new { StaticReportVersionId = staticReportVersionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            logger.DeletedStaticReportVersion(staticReportVersionId);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, StaticReportStoredProcedures.Delete);

            // spdStaticReportVersion RAISERRORs (error 50000) when the version is not current.
            throw IsUserRaisedError(exception)
                ? new ConcurrencyException(
                    $"Static report version '{staticReportVersionId}' cannot be deleted because it is not the current version.",
                    exception)
                : exception;
        }
    }

    /// <summary>
    /// Identifies the business-rule <c>RAISERROR</c>s raised by <c>spiStaticReport</c> and
    /// <c>spdStaticReportVersion</c>. The message is also matched so that providers other than
    /// SQL Server - including the fakes used in tests - can signal the same condition.
    /// </summary>
    private static bool IsUserRaisedError(DbException exception) =>
        exception is SqlException { Number: UserRaisedErrorNumber } ||
        exception.Message.Contains("cannot upload a user manual", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("cannot upload a static report", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("is not current", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<StaticReportVersion>> ReadVersionsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var versions = new List<StaticReportVersion>();

        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(new StaticReportVersion
            {
                Id = reader.GetGuid(0),
                StaticReportId = reader.GetGuid(1),
                Title = ReadString(reader, 2),
                VersionMajor = reader.GetByte(3),
                EffectiveDateFrom = reader.GetDateTime(4),
                EffectiveDateTo = ReadNullableDateTime(reader, 5),
                IsUserManual = reader.GetBoolean(6),
                IsPublic = reader.GetBoolean(7),
                FileSize = reader.GetInt32(8)
            });
        }

        return versions;
    }

    private static string ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static DateTime? ReadNullableDateTime(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

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

    /// <summary>Shape of the row returned by <c>spgStaticReportVersionData</c>.</summary>
    private sealed record StaticReportDataRow(byte[]? PdfData, bool IsPublic, string? Title);
}
