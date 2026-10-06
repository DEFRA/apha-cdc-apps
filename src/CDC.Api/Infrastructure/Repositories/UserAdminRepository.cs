using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.UserAdmin;
using CDC.Api.Features.UserAdmin.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IUserAdminRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UserAdminRepository(IDbConnectionFactory connectionFactory, ILogger<UserAdminRepository> logger)
    : IUserAdminRepository
{
    private const int RowVersionLength = 8;

    /// <summary>
    /// No legacy stored procedure returns a single user's email address alongside its roles
    /// and row version - <c>spgUser</c> omits the email address, which <c>spiSsoUser</c>
    /// requires - so the row is read with this parameterised statement instead.
    /// </summary>
    private const string SelectUserById =
        "SELECT [Id], [UserName], [FullName], [Organisation], [EmailAddress], [SubscribedToReviewEmails], " +
        "[IsProfileEditor], [IsPolicyProfileUser], [SsoUserId], [LastUpdated] FROM [dbo].[User] WHERE [Id] = @UserId";

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUser>> GetGlobalUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<MaintainedUserRow>(new CommandDefinition(
                UserAdminStoredProcedures.GetAllGlobalUsers,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(ToEntity)];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserAdminStoredProcedures.GetAllGlobalUsers);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUser>> GetExternalUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            // The procedure also returns profile and permission result sets for the wider
            // external user maintenance screen; only the user list is needed here.
            var rows = await connection.QueryAsync<MaintainedUserRow>(new CommandDefinition(
                UserAdminStoredProcedures.GetAllSsoUserMaintenance,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(ToEntity)];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserAdminStoredProcedures.GetAllSsoUserMaintenance);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MaintainedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await ReadUserAsync(connection, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> UpdateReviewEmailSubscriptionAsync(
        Guid userId,
        bool subscribed,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var user = await ReadUserAsync(connection, userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return user.IsExternal
            ? await UpdateExternalSubscriptionAsync(connection, user, subscribed, cancellationToken)
            : await UpdateGlobalSubscriptionAsync(connection, user, subscribed, lastUpdated, cancellationToken);
    }

    private async Task<byte[]> UpdateGlobalSubscriptionAsync(
        DbConnection connection,
        MaintainedUser user,
        bool subscribed,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        // spiGlobalUser is an upsert over the whole account, so every other attribute is passed
        // back exactly as it was read; only the subscription flag changes.
        var parameters = new DynamicParameters();
        parameters.Add("@UserId", user.Id, DbType.Guid);
        parameters.Add("@UserName", user.UserName, DbType.String);
        parameters.Add("@FullName", user.FullName, DbType.String);
        parameters.Add("@Organisation", user.Organisation, DbType.String);
        parameters.Add("@SubscribedToReviewEmails", subscribed, DbType.Boolean);
        parameters.Add("@IsProfileEditor", user.IsProfileEditor, DbType.Boolean);
        parameters.Add("@IsPolicyProfileUser", user.IsPolicyProfileUser, DbType.Boolean);
        parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UserAdminStoredProcedures.UpsertGlobalUser,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserAdminStoredProcedures.UpsertGlobalUser);

            throw SpeciesRepository.IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"User '{user.Id}' has been edited by another user. Reload the user and try again.",
                    exception)
                : exception;
        }
    }

    private async Task<byte[]> UpdateExternalSubscriptionAsync(
        DbConnection connection,
        MaintainedUser user,
        bool subscribed,
        CancellationToken cancellationToken)
    {
        try
        {
            // spiSsoUser is keyed on SsoUserId and takes no row version, exactly as the legacy
            // external user screen saved it, so the new version is re-read afterwards.
            await connection.ExecuteAsync(new CommandDefinition(
                UserAdminStoredProcedures.UpsertSsoUser,
                new
                {
                    user.UserName,
                    user.FullName,
                    user.EmailAddress,
                    user.Organisation,
                    SubscribedToEmails = subscribed,
                    user.SsoUserId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserAdminStoredProcedures.UpsertSsoUser);
            throw;
        }

        var refreshed = await ReadUserAsync(connection, user.Id, cancellationToken);

        return refreshed?.LastUpdated ?? [];
    }

    private async Task<MaintainedUser?> ReadUserAsync(
        DbConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<MaintainedUserRow>(new CommandDefinition(
                SelectUserById,
                new { UserId = userId },
                cancellationToken: cancellationToken));

            return row is null ? null : ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, nameof(SelectUserById));
            throw;
        }
    }

    private static MaintainedUser ToEntity(MaintainedUserRow row) => new()
    {
        Id = row.Id,
        UserName = row.UserName ?? string.Empty,
        FullName = row.FullName ?? string.Empty,
        Organisation = row.Organisation ?? string.Empty,
        EmailAddress = row.EmailAddress,
        SubscribedToReviewEmails = row.SubscribedToReviewEmails,
        IsProfileEditor = row.IsProfileEditor,
        IsPolicyProfileUser = row.IsPolicyProfileUser,
        SsoUserId = row.SsoUserId ?? Guid.Empty,
        LastUpdated = row.LastUpdated ?? []
    };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(UserAdminRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    /// <summary>Row shape shared by the global user, external user and single user reads.</summary>
    private sealed class MaintainedUserRow
    {
        public Guid Id { get; init; }

        public string? UserName { get; init; }

        public string? FullName { get; init; }

        public string? Organisation { get; init; }

        public string? EmailAddress { get; init; }

        public bool SubscribedToReviewEmails { get; init; }

        public bool IsProfileEditor { get; init; }

        public bool IsPolicyProfileUser { get; init; }

        public Guid? SsoUserId { get; init; }

        public byte[]? LastUpdated { get; init; }
    }
}
