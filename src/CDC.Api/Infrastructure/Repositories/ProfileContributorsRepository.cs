using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileContributors;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileContributorsRepository"/>.
/// </summary>
/// <remarks>
/// <c>spgProfileContributorsByProfileId</c> returns a second result set (per-section review
/// email preferences) that this read-only contributors list does not need, so it is left unread -
/// Dapper's <see cref="SqlMapper.QueryAsync{T}(IDbConnection, CommandDefinition)"/> only consumes
/// the first result set.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileContributorsRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileContributorsRepository> logger)
    : IProfileContributorsRepository
{
    /// <summary>SQL Server reports <c>RAISERROR</c> with a user-defined message as error 50000.</summary>
    private const int UserRaisedErrorNumber = 50000;

    /// <summary>The message <c>spiProfileContributor</c> raises when the row version has moved on.</summary>
    private const string ConcurrencyMessageFragment = "edited by another user";

    /// <summary>The message <c>spiProfileContributor</c> raises when the username is already in use.</summary>
    private const string DuplicateUsernameMessageFragment = "already a user with the specified username";

    private const int RowVersionLength = 8;

    /// <summary><c>spiProfileContributor</c> declares <c>@UserName varchar(50)</c>.</summary>
    private const int UserNameMaxLength = 50;

    /// <summary><c>spiProfileContributor</c> declares <c>@FullName</c>/<c>@Organisation varchar(100)</c>.</summary>
    private const int NameMaxLength = 100;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Contributor>> GetProfileContributorsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ContributorRow>(new CommandDefinition(
                ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId,
                new { ProfileId = profileId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new Contributor
            {
                Id = row.Id,
                UserName = row.UserName,
                FullName = row.FullName,
                Organisation = row.Organisation,
                Role = row.Name,
                LastUpdated = row.LastUpdated
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ContributorEdit?> GetContributorForEditAsync(Guid profileId, Guid contributorId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            return await ReadContributorForEditAsync(connection, null, profileId, contributorId, cancellationToken);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.GetContributor);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileUserRole>> GetProfileUserRolesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ProfileUserRoleRow>(new CommandDefinition(
                ProfileContributorsStoredProcedures.GetProfileUserRoles,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ProfileUserRole
            {
                Id = row.Id,
                Name = row.Name,
                IsContributor = row.IsContributor
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.GetProfileUserRoles);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> UpdateContributorAsync(UpdateContributorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var contributor = await ReadContributorForEditAsync(
                connection, transaction, command.ProfileId, command.ContributorId, cancellationToken);

            if (contributor is null)
            {
                return null;
            }

            var newLastUpdated = await UpsertContributorCoreAsync(
                connection,
                transaction,
                new UpsertContributorParameters(
                    command.ContributorId,
                    contributor.UserName,
                    contributor.IsSsoUser ? contributor.FullName : command.FullName,
                    contributor.IsSsoUser ? contributor.Organisation : command.Organisation,
                    command.RoleId,
                    command.ProfileId,
                    command.LastUpdated),
                cancellationToken);

            var toAdd = command.SectionPermissionIds.Except(contributor.SectionPermissionIds);
            var toRemove = contributor.SectionPermissionIds.Except(command.SectionPermissionIds);

            foreach (var sectionId in toAdd)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileContributorsStoredProcedures.AddSectionPermission,
                    new { UserId = command.ContributorId, command.ProfileId, ProfileSectionId = sectionId },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            foreach (var sectionId in toRemove)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileContributorsStoredProcedures.RemoveSectionPermission,
                    new { UserId = command.ContributorId, command.ProfileId, ProfileSectionId = sectionId },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);

            return newLastUpdated;
        }
        catch (DbException exception)
        {
            // Disposing an uncommitted transaction rolls it back, but rolling back explicitly
            // releases the locks immediately and makes the failure path unambiguous.
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.UpsertContributor);

            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Contributor '{command.ContributorId}' has been edited by another user. Re-read the contributor and try again.",
                    exception)
                : exception;
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(transaction, CancellationToken.None);
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
                ProfileContributorsStoredProcedures.FindUserByUsername,
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
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.FindUserByUsername);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddContributorAsync(AddContributorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingContributor = await ReadContributorForEditAsync(
                connection, transaction, command.ProfileId, command.ContributorId, cancellationToken);

            var userName = existingContributor?.UserName ?? command.UserName;
            var isSsoUser = existingContributor?.IsSsoUser ?? false;
            var fullName = isSsoUser ? existingContributor!.FullName : command.FullName;
            var organisation = isSsoUser ? existingContributor!.Organisation : command.Organisation;
            var lastUpdated = existingContributor?.LastUpdated ?? new byte[RowVersionLength];
            var existingSectionPermissionIds = existingContributor?.SectionPermissionIds ?? [];

            await UpsertContributorCoreAsync(
                connection,
                transaction,
                new UpsertContributorParameters(command.ContributorId, userName, fullName, organisation, command.RoleId, command.ProfileId, lastUpdated),
                cancellationToken);

            foreach (var sectionId in command.SectionPermissionIds.Except(existingSectionPermissionIds))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileContributorsStoredProcedures.AddSectionPermission,
                    new { UserId = command.ContributorId, command.ProfileId, ProfileSectionId = sectionId },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.UpsertContributor);

            if (IsDuplicateUsernameViolation(exception))
            {
                throw new DuplicateUsernameException($"The username '{command.UserName}' is already in use.", exception);
            }

            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Contributor '{command.ContributorId}' has been edited by another user. Re-read the contributor and try again.",
                    exception)
                : exception;
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteContributorAsync(Guid profileId, Guid contributorId, byte[] lastUpdated, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserId", contributorId, DbType.Guid);
            parameters.Add("@ProfileId", profileId, DbType.Guid);
            parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileContributorsStoredProcedures.DeleteContributor,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileContributorsStoredProcedures.DeleteContributor);

            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Contributor '{contributorId}' has been edited by another user. Re-read the contributor and try again.",
                    exception)
                : exception;
        }
    }

    /// <summary>Reads one contributor's editable detail and granted section permissions via <c>spgContributor</c>.</summary>
    private static async Task<ContributorEdit?> ReadContributorForEditAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid profileId,
        Guid contributorId,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
            ProfileContributorsStoredProcedures.GetContributor,
            new { UserId = contributorId, ProfileId = profileId },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetGuid(0);
        var userName = reader.GetString(1);
        var fullName = reader.GetString(2);
        var organisation = reader.GetString(3);
        var roleId = reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4);
        var isSsoUser = !reader.IsDBNull(7);
        var lastUpdated = (byte[])reader.GetValue(8);

        // The permissions result set only exists when the contributor is a ProfileUser on this
        // profile (the branch of spgContributor that also returns a non-null role).
        var sectionPermissionIds = new List<Guid>();

        if (roleId.HasValue && await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                sectionPermissionIds.Add(reader.GetGuid(0));
            }
        }

        return new ContributorEdit
        {
            Id = id,
            UserName = userName,
            FullName = fullName,
            Organisation = organisation,
            RoleId = roleId ?? Guid.Empty,
            IsSsoUser = isSsoUser,
            SectionPermissionIds = sectionPermissionIds,
            LastUpdated = lastUpdated
        };
    }

    /// <summary>Bundles <see cref="UpsertContributorCoreAsync"/>'s parameters so the method itself
    /// stays within the project's authorised parameter-count limit.</summary>
    private sealed record UpsertContributorParameters(
        Guid UserId,
        string UserName,
        string FullName,
        string Organisation,
        Guid RoleId,
        Guid ProfileId,
        byte[] LastUpdated);

    /// <summary>
    /// Upserts the <c>User</c>/<c>ProfileUser</c> row via <c>spiProfileContributor</c>, which
    /// also checks the row version and raises an error (translated to
    /// <see cref="ConcurrencyException"/>/<see cref="DuplicateUsernameException"/> by the caller)
    /// when it has moved on or the username is already in use.
    /// </summary>
    private static async Task<byte[]> UpsertContributorCoreAsync(
        DbConnection connection,
        DbTransaction transaction,
        UpsertContributorParameters parameters,
        CancellationToken cancellationToken)
    {
        var dynamicParameters = new DynamicParameters();
        dynamicParameters.Add("@UserId", parameters.UserId, DbType.Guid);
        dynamicParameters.Add("@UserName", parameters.UserName, DbType.AnsiString, size: UserNameMaxLength);
        dynamicParameters.Add("@FullName", parameters.FullName, DbType.AnsiString, size: NameMaxLength);
        dynamicParameters.Add("@Organisation", parameters.Organisation, DbType.AnsiString, size: NameMaxLength);
        dynamicParameters.Add("@RoleId", parameters.RoleId, DbType.Guid);
        dynamicParameters.Add("@ProfileId", parameters.ProfileId, DbType.Guid);
        dynamicParameters.Add("@LastUpdated", parameters.LastUpdated, DbType.Binary, size: RowVersionLength);
        dynamicParameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileContributorsStoredProcedures.UpsertContributor,
            dynamicParameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return dynamicParameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    private static async Task RollbackAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (DbException)
        {
            // The connection may already be broken by the failure that triggered the rollback;
            // disposing the transaction/connection afterwards is enough to release resources.
        }
    }

    /// <summary>
    /// Identifies the row version clash raised by <c>spiProfileContributor</c>. The message is
    /// also matched so that providers other than SQL Server - including the fakes used in
    /// tests - can signal the same condition.
    /// </summary>
    private static bool IsConcurrencyViolation(DbException exception) =>
        exception is SqlException { Number: UserRaisedErrorNumber } ||
        exception.Message.Contains(ConcurrencyMessageFragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>Identifies the duplicate-username clash raised by <c>spiProfileContributor</c> when
    /// inserting a brand-new global user whose username is already taken.</summary>
    private static bool IsDuplicateUsernameViolation(DbException exception) =>
        exception.Message.Contains(DuplicateUsernameMessageFragment, StringComparison.OrdinalIgnoreCase);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ProfileContributorsRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    // Column names map by Dapper convention (see e.g. ProfileManagementRepository.ProfileStatusTypeRow).
    // Dapper's constructor-based materialization requires one parameter per column actually
    // returned by spgProfileContributorsByProfileId's first result set - ProfileUserRoleId,
    // IsContributor and SsoUserId are unused by the mapping below but must still be declared.
    // The role name column is unaliased in the stored procedure (it selects
    // [luProfileUserRole].[Name]), so it arrives here as "Name" and is renamed to "Role" above.
    private sealed record ContributorRow(
        Guid Id,
        string UserName,
        string FullName,
        string Organisation,
        Guid ProfileUserRoleId,
        string Name,
        bool IsContributor,
        Guid? SsoUserId,
        byte[] LastUpdated);

    // The stored procedure selects [Name] (unaliased), so it arrives here as "Name".
    private sealed record ProfileUserRoleRow(Guid Id, string Name, bool IsContributor);
}

