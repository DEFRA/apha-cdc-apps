using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users;
using CDC.Api.Features.Users.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IUserRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UserRepository(IDbConnectionFactory connectionFactory, ILogger<UserRepository> logger)
    : IUserRepository
{
    /// <inheritdoc />
    public async Task<ExternalUser?> GetBySsoUserIdExtAsync(Guid ssoUserIdExt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
                UserStoredProcedures.GetUserAuthorisation,
                new { SsoUserIdExt = ssoUserIdExt },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.GetUserAuthorisation);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExternalUser?> GetByEmailAddressAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
                UserStoredProcedures.GetByEmailAddress,
                new { EmailAddress = email },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.GetByEmailAddress);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateSsoUserIdExtAsync(Guid id, Guid ssoUserIdExt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UserStoredProcedures.UpdateSsoUserIdExt,
                new { Id = id, SsoUserIdExt = ssoUserIdExt },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.UpdateSsoUserIdExt);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExternalUser> CreateExternalUserAsync(ExternalUser newUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newUser);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UserStoredProcedures.CreateExternalUser,
                new
                {
                    newUser.Id,
                    newUser.UserName,
                    newUser.FullName,
                    newUser.Organisation,
                    newUser.EmailAddress,
                    newUser.SsoUserIdExt
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return newUser;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.CreateExternalUser);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<InternalUser?> GetBySsoUserIdIntAsync(Guid ssoUserIdInt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<InternalUserRow>(new CommandDefinition(
                UserStoredProcedures.GetUserAuthorisation,
                new { SsoUserIdInt = ssoUserIdInt },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.GetUserAuthorisation);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<InternalUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<InternalUserRow>(new CommandDefinition(
                UserStoredProcedures.GetUserAuthorisation,
                new { UserName = userName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.GetUserAuthorisation);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateSsoUserIdIntAsync(Guid id, Guid ssoUserIdInt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UserStoredProcedures.UpdateSsoUserIdInt,
                new { Id = id, SsoUserIdInt = ssoUserIdInt },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.UpdateSsoUserIdInt);
            throw;
        }
    }

    // SsoUserIdExt, SsoUserId and EmailAddress are nullable in the database (every internal row has
    // none of them) - a row returned by GetBySsoUserIdExtAsync/GetByEmailAddressAsync always has
    // SsoUserIdExt and EmailAddress, since both procedures filter on one of them, but the mapped
    // type must still declare them nullable.
    private static ExternalUser? ToEntity(UserRow? row) => row is null
        ? null
        : new ExternalUser
        {
            Id = row.Id,
            UserName = row.UserName ?? string.Empty,
            FullName = row.FullName ?? string.Empty,
            Organisation = row.Organisation ?? string.Empty,
            EmailAddress = row.EmailAddress ?? string.Empty,
            SsoUserIdExt = row.SsoUserIdExt ?? Guid.Empty,
            SsoUserId = row.SsoUserId,
            IsProfileEditor = row.IsProfileEditor,
            IsPolicyProfileUser = row.IsPolicyProfileUser
        };

    // SsoUserIdInt is nullable in the database (every external/legacy row has none of it) - a row
    // returned by GetBySsoUserIdIntAsync/GetByUserNameAsync always has UserName, since both calls
    // filter on one of them, but the mapped type must still declare it nullable.
    private static InternalUser? ToEntity(InternalUserRow? row) => row is null
        ? null
        : new InternalUser
        {
            Id = row.Id,
            UserName = row.UserName ?? string.Empty,
            FullName = row.FullName ?? string.Empty,
            SsoUserIdInt = row.SsoUserIdInt,
            IsProfileEditor = row.IsProfileEditor,
            IsPolicyProfileUser = row.IsPolicyProfileUser
        };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(UserRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    private sealed record UserRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? UserName { get; init; } // NOSONAR
        public string? FullName { get; init; } // NOSONAR
        public string? Organisation { get; init; } // NOSONAR
        public string? EmailAddress { get; init; } // NOSONAR
        public Guid? SsoUserIdExt { get; init; } // NOSONAR
        public Guid? SsoUserId { get; init; } // NOSONAR
        public bool IsProfileEditor { get; init; } // NOSONAR
        public bool IsPolicyProfileUser { get; init; } // NOSONAR
    }

    private sealed record InternalUserRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? UserName { get; init; } // NOSONAR
        public string? FullName { get; init; } // NOSONAR
        public Guid? SsoUserIdInt { get; init; } // NOSONAR
        public bool IsProfileEditor { get; init; } // NOSONAR
        public bool IsPolicyProfileUser { get; init; } // NOSONAR
    }
}
