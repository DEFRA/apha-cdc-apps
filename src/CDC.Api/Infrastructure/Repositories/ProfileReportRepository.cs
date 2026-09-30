using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileReportRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileReportRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileReportRepository> logger)
    : IProfileReportRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileVersionReport>> GetProfileVersionReportsAsync(
        Guid profileVersionId,
        bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ProfileVersionReportRow>(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileVersionReports,
                new { ProfileVersionId = profileVersionId, IsAuthenticated = isAuthenticated },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ProfileVersionReport
            {
                Id = row.Id,
                ReportName = row.ReportName ?? string.Empty,
                DisplayName = row.DisplayName ?? string.Empty,
                HasPdfData = row.HasPdfData,
                FileSize = row.FileSize
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetProfileVersionReports);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileReportData?> GetProfileReportDataAsync(
        Guid profileVersionId,
        Guid profileReportId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var reportData = await connection.QuerySingleOrDefaultAsync<byte[]?>(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileVersionReportData,
                new { ProfileVersionId = profileVersionId, ProfileReportId = profileReportId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return reportData is null ? null : new ProfileReportData { ReportData = reportData };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetProfileVersionReportData);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Guid> CreateProfileReportAsync(CreateProfileReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileReportStoredProcedures.InsertProfileVersionReportData,
                new { command.ProfileVersionId, command.ProfileReportId, PdfData = command.ReportData },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return command.ProfileReportId;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.InsertProfileVersionReportData);
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
                $"{nameof(ProfileReportRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    /// <summary>Row shape returned by <c>spgProfileVersionReportByProfileVersionId</c>; column names map by Dapper convention.</summary>
    private sealed record ProfileVersionReportRow(Guid Id, string? ReportName, string? DisplayName, bool HasPdfData, int FileSize);
}
