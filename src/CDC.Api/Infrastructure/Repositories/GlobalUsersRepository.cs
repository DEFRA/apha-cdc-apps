using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.GlobalUsers;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Common.Contracts;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IGlobalUsersRepository"/>, using the stored procedures the
/// legacy <c>GlobalUserList</c> CSLA object called.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class GlobalUsersRepository(IDbConnectionFactory connectionFactory, ILogger<GlobalUsersRepository> logger)
    : IGlobalUsersRepository
{
    private const string GetAllProcedure = "spgaGlobalUser";
    private const string GetUserProcedure = "spgUser";
    private const string FindByUsernameProcedure = "spgUserAuthorisation";
    private const string SaveProcedure = "spiGlobalUser";
    private const string DeleteProcedure = "spdGlobalUser";

    /// <summary>The message the stored procedures raise when the row version has moved on.</summary>
    private const string ConcurrencyMessageFragment = "edited by another user";

    /// <summary>The message <c>spiGlobalUser</c> raises when a new user's username is already in use.</summary>
    private const string DuplicateUsernameMessageFragment = "already a user with the specified username";

    private const int RowVersionLength = 8;
    private const int UserNameMaxLength = 50;
    private const int NameMaxLength = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlobalUser>> GetGlobalUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                GetAllProcedure,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var users = new List<GlobalUser>();

            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new GlobalUser
                {
                    Id = reader.GetGuid(0),
                    UserName = reader.GetString(1),
                    FullName = reader.GetString(2),
                    Organisation = reader.GetString(3),
                    SubscribedToReviewEmails = reader.GetBoolean(4),
                    Role = ToRole(reader.GetBoolean(5), reader.GetBoolean(6)),
                    LastUpdated = (byte[])reader.GetValue(reader.GetOrdinal("LastUpdated"))
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
    public async Task<GlobalUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                GetUserProcedure,
                new { UserId = userId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var ssoUserId = reader.IsDBNull(7) ? Guid.Empty : reader.GetGuid(7);

            return new GlobalUser
            {
                Id = reader.GetGuid(0),
                UserName = reader.GetString(1),
                FullName = reader.GetString(2),
                Organisation = reader.GetString(3),
                SubscribedToReviewEmails = reader.GetBoolean(4),
                Role = ToRole(reader.GetBoolean(5), reader.GetBoolean(6)),
                IsSsoUser = ssoUserId != Guid.Empty,
                LastUpdated = (byte[])reader.GetValue(reader.GetOrdinal("LastUpdated"))
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, GetUserProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserVerification?> FindUserByUsernameAsync(string userName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                FindByUsernameProcedure,
                new { UserName = userName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new UserVerification
            {
                Id = reader.GetGuid(0),
                IsUserManagementSystem = reader.GetBoolean(7)
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, FindByUsernameProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveGlobalUserAsync(GlobalUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserId", user.Id, DbType.Guid);
            parameters.Add("@UserName", user.UserName, DbType.AnsiString, size: UserNameMaxLength);
            parameters.Add("@FullName", user.FullName, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@Organisation", user.Organisation, DbType.AnsiString, size: NameMaxLength);
            parameters.Add("@SubscribedToReviewEmails", user.SubscribedToReviewEmails, DbType.Boolean);
            parameters.Add("@IsProfileEditor", user.Role == GlobalUserRole.ProfileEditor, DbType.Boolean);
            parameters.Add("@IsPolicyProfileUser", user.Role == GlobalUserRole.PolicyProfileUser, DbType.Boolean);
            parameters.Add("@LastUpdated", user.LastUpdated, DbType.Binary, size: RowVersionLength);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                SaveProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SaveProcedure);

            if (HasMessage(exception, DuplicateUsernameMessageFragment))
            {
                throw new DuplicateUsernameException($"The username '{user.UserName}' is already in use.", exception);
            }

            throw HasMessage(exception, ConcurrencyMessageFragment)
                ? new ConcurrencyException($"Global user '{user.Id}' has been edited by another user. Re-read the user and try again.", exception)
                : exception;
        }
    }

    /// <inheritdoc />
    public async Task DeleteGlobalUserAsync(Guid userId, byte[] lastUpdated, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserId", userId, DbType.Guid);
            parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                DeleteProcedure,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DeleteProcedure);

            throw HasMessage(exception, ConcurrencyMessageFragment)
                ? new ConcurrencyException($"Global user '{userId}' has been edited by another user. Re-read the user and try again.", exception)
                : exception;
        }
    }

    private static GlobalUserRole ToRole(bool isProfileEditor, bool isPolicyProfileUser) =>
        isProfileEditor ? GlobalUserRole.ProfileEditor
        : isPolicyProfileUser ? GlobalUserRole.PolicyProfileUser
        : GlobalUserRole.None;

    private static bool HasMessage(DbException exception, string fragment) =>
        exception.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(GlobalUsersRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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
