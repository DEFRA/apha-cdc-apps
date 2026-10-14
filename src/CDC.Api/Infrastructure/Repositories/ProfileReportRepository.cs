using System.Data;
using System.Data.Common;
using System.Globalization;
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
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileVersionReports,
                new { ProfileVersionId = profileVersionId, IsAuthenticated = isAuthenticated },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // Id, ReportName, DisplayName, HasPdfData, FileSize (NULL until a PDF is stored), DisplayPublic.
            var reports = new List<ProfileVersionReport>();
            while (await reader.ReadAsync(cancellationToken))
            {
                reports.Add(new ProfileVersionReport
                {
                    Id = reader.GetGuid(0),
                    ReportName = reader.ReadString(1),
                    DisplayName = reader.ReadString(2),
                    HasPdfData = reader.ReadBoolean(3),
                    FileSize = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)
                });
            }

            return reports;
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<DraftProfileVersion>> GetDraftProfileVersionsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileHistory,
                new { ProfileId = profileId, StateName = "Draft", PublicOnly = false },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // First result set: Id, ProfileId, Title, VersionMajor, VersionMinor, State, EffectiveDateFrom,
            // EffectiveDateTo, IsLatestVersion, ScenarioTitle, ParentId, IsPublic, ProfileStatusId, ProfileStatusName.
            var versions = new List<DraftProfileVersion>();
            while (await reader.ReadAsync(cancellationToken))
            {
                versions.Add(new DraftProfileVersion
                {
                    Id = reader.GetGuid(0),
                    Title = reader.ReadString(2),
                    VersionMajor = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    VersionMinor = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    EffectiveDateFrom = reader.GetDateTime(6),
                    EffectiveDateTo = reader.ReadNullableDateTime(7),
                    ScenarioTitle = reader.ReadString(9),
                    ProfileStatusName = reader.ReadString(13)
                });
            }

            return versions;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetProfileHistory);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileVersionHeader?> GetProfileVersionHeaderAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileVersionInfoById,
                new { Id = profileVersionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // First result set: Id, ProfileId, Title, VersionMajor, VersionMinor, State, EffectiveDateFrom,
            // EffectiveDateTo, IsLatestVersion, ScenarioTitle, ParentId, IsPublic, ProfileStatusId, ProfileStatusName.
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ProfileVersionHeader
            {
                Id = reader.GetGuid(0),
                ProfileId = reader.ReadGuid(1),
                Title = reader.ReadString(2),
                VersionMajor = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                VersionMinor = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                IsPublished = !string.Equals(reader.ReadString(5), "Draft", StringComparison.Ordinal),
                EffectiveDateFrom = reader.GetDateTime(6),
                EffectiveDateTo = reader.ReadNullableDateTime(7),
                ScenarioTitle = reader.ReadString(9),
                IsPublic = reader.ReadBoolean(11),
                ProfileStatusName = reader.ReadString(13)
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetProfileVersionInfoById);
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
}
