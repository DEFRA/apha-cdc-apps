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
    public async Task<ExternalUser?> GetByCidmSsoIdAsync(Guid cidmSsoId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
                UserStoredProcedures.GetByCidmSsoId,
                new { CidmSsoId = cidmSsoId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return ToEntity(row);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.GetByCidmSsoId);
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
    public async Task UpdateCidmSsoIdAsync(Guid id, Guid cidmSsoId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                UserStoredProcedures.UpdateCidmSsoId,
                new { Id = id, CidmSsoId = cidmSsoId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, UserStoredProcedures.UpdateCidmSsoId);
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
                    newUser.CidmSsoId
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

    // CidmSsoId, SsoUserId and EmailAddress are nullable in the database (every internal row has
    // none of them) - a row returned by GetByCidmSsoId/GetByEmailAddress always has CidmSsoId and
    // EmailAddress, since both procedures filter on one of them, but the mapped type must still
    // declare them nullable.
    private static ExternalUser? ToEntity(UserRow? row) => row is null
        ? null
        : new ExternalUser
        {
            Id = row.Id,
            UserName = row.UserName ?? string.Empty,
            FullName = row.FullName ?? string.Empty,
            Organisation = row.Organisation ?? string.Empty,
            EmailAddress = row.EmailAddress ?? string.Empty,
            CidmSsoId = row.CidmSsoId ?? Guid.Empty,
            SsoUserId = row.SsoUserId,
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
        public Guid? CidmSsoId { get; init; } // NOSONAR
        public Guid? SsoUserId { get; init; } // NOSONAR
        public bool IsProfileEditor { get; init; } // NOSONAR
        public bool IsPolicyProfileUser { get; init; } // NOSONAR
    }
}
