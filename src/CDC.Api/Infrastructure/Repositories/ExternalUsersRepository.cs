using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.ExternalUsers.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IExternalUsersRepository"/>, using the stored procedures the
/// legacy <c>SsoUserMaintenance</c> CSLA object called.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ExternalUsersRepository(IDbConnectionFactory connectionFactory, ILogger<ExternalUsersRepository> logger)
    : IExternalUsersRepository
{
    private const string GetAllProcedure = "spgaSsoUserMaintenance";
    private const string SaveProcedure = "spiSsoUser";
    private const string PreRegisterProcedure = "spiExternalUserPreRegistration";
    private const string AwaitingInstructionsProcedure = "spgaExternalUsersAwaitingLoginInstructions";
    private const string LoginRecipientProcedure = "spgExternalUserLoginInstructions";
    private const string MarkInstructionsSentProcedure = "spuExternalUserLoginInstructionsSent";
    private const int UserNameMaxLength = 50;
    private const int EmailMaxLength = 50;
    private const int NameMaxLength = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalUserAccount>> GetExternalUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                GetAllProcedure,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var users = new List<ExternalUserAccount>();

            // Only the first result set (the users) is needed; the profile result sets are ignored.
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new ExternalUserAccount
                {
                    Id = reader.GetGuid(0),
                    UserName = reader.GetString(1),
                    FullName = reader.GetString(2),
                    EmailAddress = reader.GetString(3),
                    Organisation = reader.GetString(4),
                    SubscribedToReviewEmails = reader.GetBoolean(5),
                    SsoUserId = reader.GetGuid(6)
                });
            }

            return users;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, GetAllProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExternalUserProfileAccess> GetProfileAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                GetAllProcedure,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // Result sets: users (skipped), every profile, every external user's profile roles, their section permissions.
            var allProfiles = new List<ProfileOption>();
            var grants = new List<GrantRow>();
            var sections = new List<(Guid ProfileId, Guid SectionId)>();

            await reader.NextResultAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                allProfiles.Add(new ProfileOption { Id = reader.GetGuid(0), Title = ReadString(reader, 1) });
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.GetGuid(0) == userId)
                    {
                        grants.Add(new GrantRow(reader.GetGuid(1), ReadString(reader, 2), reader.GetGuid(3), ReadString(reader, 4), reader.GetBoolean(5)));
                    }
                }
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.GetGuid(0) == userId)
                    {
                        sections.Add((reader.GetGuid(1), reader.GetGuid(2)));
                    }
                }
            }

            var grantedProfileIds = grants.Select(grant => grant.ProfileId).ToHashSet();

            return new ExternalUserProfileAccess
            {
                Profiles = [.. grants.Select(grant => new ExternalUserProfileGrant
                {
                    ProfileId = grant.ProfileId,
                    Title = grant.Title,
                    RoleId = grant.RoleId,
                    RoleName = grant.RoleName,
                    IsContributor = grant.IsContributor,
                    SectionPermissionIds = [.. sections.Where(section => section.ProfileId == grant.ProfileId).Select(section => section.SectionId)]
                })],
                AvailableProfiles = [.. allProfiles.Where(profile => !grantedProfileIds.Contains(profile.Id))]
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, GetAllProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PreRegisterExternalUserAsync(
        Guid id, string userName, string fullName, string organisation, string emailAddress, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", id, DbType.Guid);
            parameters.Add("@UserName", userName, DbType.AnsiString, size: UserNameMaxLength);
            parameters.Add("@FullName", fullName, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@Organisation", organisation, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@EmailAddress", emailAddress, DbType.AnsiString, size: EmailMaxLength);

            return await connection.QuerySingleAsync<bool>(new CommandDefinition(
                PreRegisterProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PreRegisterProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveExternalUserAsync(ExternalUserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserName", user.UserName, DbType.AnsiString, size: UserNameMaxLength);
            parameters.Add("@FullName", user.FullName, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@EmailAddress", user.EmailAddress, DbType.AnsiString, size: EmailMaxLength);
            parameters.Add("@Organisation", user.Organisation, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@SubscribedToEmails", user.SubscribedToReviewEmails, DbType.Boolean);
            parameters.Add("@SsoUserId", user.SsoUserId, DbType.Guid);

            await connection.ExecuteAsync(new CommandDefinition(
                SaveProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SaveProcedure);
            throw;
        }
    }

    private sealed record GrantRow(Guid ProfileId, string Title, Guid RoleId, string RoleName, bool IsContributor);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetUsersAwaitingLoginInstructionsAsync(CancellationToken cancellationToken) =>
        await RunAsync(
            AwaitingInstructionsProcedure,
            async connection => (await connection.QueryAsync<Guid>(new CommandDefinition(
                AwaitingInstructionsProcedure,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToHashSet(),
            cancellationToken);

    /// <inheritdoc />
    public Task<ExternalUserLoginRecipient?> GetLoginRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Id", userId, DbType.Guid);

        return RunAsync(
            LoginRecipientProcedure,
            connection => connection.QuerySingleOrDefaultAsync<ExternalUserLoginRecipient>(new CommandDefinition(
                LoginRecipientProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> MarkLoginInstructionsSentAsync(Guid userId, string emailAddress, DateTime sentDate, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Id", userId, DbType.Guid);
        parameters.Add("@EmailAddress", emailAddress, DbType.AnsiString, size: EmailMaxLength);
        parameters.Add("@SentDate", sentDate, DbType.DateTime2);

        return RunAsync(
            MarkInstructionsSentProcedure,
            connection => connection.QuerySingleAsync<bool>(new CommandDefinition(
                MarkInstructionsSentProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)),
            cancellationToken);
    }

    private async Task<T> RunAsync<T>(string procedure, Func<DbConnection, Task<T>> query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            return await query(connection);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, procedure);
            throw;
        }
    }

    // A child profile with no scenario title yields a NULL concatenated title.
    private static string ReadString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ExternalUsersRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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
