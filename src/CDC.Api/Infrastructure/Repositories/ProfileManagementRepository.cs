using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileManagementRepository"/>.
/// </summary>
/// <remarks>
/// <para>
/// Optional string/GUID stored procedure parameters that the legacy VB.NET layer omitted when
/// empty (relying on the procedure's own <c>NULL</c> default) are instead always supplied here,
/// passing <see langword="null"/> in their place. This is behaviourally equivalent for every
/// procedure used - each declares its optional parameters with a <c>NULL</c> default - and lets
/// every call use a single, consistent parameter set.
/// </para>
/// <para>
/// <c>spgProfile</c> and <c>spgProfileVersionInfoById</c> return result sets containing more
/// columns than any single legacy call site read, so both are read positionally through a data
/// reader, exactly as <c>SpeciesRepository</c> and <c>ProfileRepository</c> already do, rather
/// than relying on Dapper's name-based mapping.
/// </para>
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileManagementRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileManagementRepository> logger)
    : IProfileManagementRepository
{
    private const int RowVersionLength = 8;
    private const string ProfiledSpeciesType = "Profiled";

    /// <inheritdoc />
    public async Task<ProfileCreationResult> CreateProfileAsync(CreateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var newLastUpdated = await InsertProfileAsync(connection, transaction, command, cancellationToken);
            await InsertInitialProfileVersionAsync(connection, transaction, command, cancellationToken);

            foreach (var species in command.AffectedSpeciesInsertList)
            {
                await InsertAffectedSpeciesAsync(connection, transaction, species, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return new ProfileCreationResult { NewProfileId = command.Id, NewLastUpdated = newLastUpdated };
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.InsertProfile);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> UpdateProfileAttributesAsync(
        UpdateProfileAttributesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@ProfileId", command.Id, DbType.Guid);
            parameters.Add("@Title", command.Title.Length > 0 ? command.Title : null, DbType.String);
            parameters.Add("@ScenarioTitle", command.ScenarioTitle.Length > 0 ? command.ScenarioTitle : null, DbType.String);
            parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.UpdateProfile,
                parameters,
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            foreach (var species in command.AffectedSpeciesDeleteList)
            {
                await DeleteAffectedSpeciesAsync(connection, transaction, species, cancellationToken);
            }

            foreach (var species in command.AffectedSpeciesInsertList)
            {
                await InsertAffectedSpeciesAsync(connection, transaction, species, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);

            throw SpeciesRepository.IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Profile '{command.Id}' has been edited by another user. Re-read the profile and try again.",
                    exception)
                : exception;
        }
    }

    /// <inheritdoc />
    public async Task<DeleteProfileVersionResult?> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedSpeciesIds = await ReadAffectedSpeciesIdsForDeletionAsync(
                connection,
                transaction,
                profileVersionId,
                cancellationToken);

            if (affectedSpeciesIds is null)
            {
                await RollbackAsync(transaction, cancellationToken);
                return null;
            }

            foreach (var speciesId in affectedSpeciesIds)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileManagementStoredProcedures.DeleteProfileVersionSpecies,
                    new { ProfileVersionId = profileVersionId, SpeciesId = speciesId },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            var parameters = new DynamicParameters();
            parameters.Add("@ProfileVersionId", profileVersionId, DbType.Guid);
            parameters.Add("@NextLatestProfileVersionId", null, DbType.Guid, ParameterDirection.Output);
            parameters.Add("@ProfileDeleted", null, DbType.Boolean, ParameterDirection.Output);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.DeleteProfileVersion,
                parameters,
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return new DeleteProfileVersionResult
            {
                NextLatestProfileVersionId = parameters.Get<Guid?>("@NextLatestProfileVersionId"),
                IsProfileDeleted = parameters.Get<bool?>("@ProfileDeleted") ?? false
            };
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.DeleteProfileVersion);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Guid> CreateNewProfileVersionAsync(CreateNewProfileVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var newProfileVersionId = await ExecuteNewProfileVersionWorkflowAsync(connection, transaction, command, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return newProfileVersionId;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.GetProfileVersionInfoById);
            throw;
        }
        catch (ConcurrencyException)
        {
            await RollbackAsync(transaction, cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileManagementStoredProcedures.GetProfile,
                    new { ProfileId = profileId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var profile = new Profile
            {
                Id = ReadGuid(reader, 0),
                Title = ReadString(reader, 1),
                ScenarioTitle = ReadString(reader, 2),
                ParentId = ReadGuid(reader, 3),
                ParentTitle = ReadString(reader, 4),
                CurrentDraftProfileVersionId = ReadGuid(reader, 5),
                CurrentPublishedProfileVersionId = ReadGuid(reader, 6),
                CurrentPublicVersionId = ReadGuid(reader, 7),
                HasPublicScenarios = ReadBoolean(reader, 8),
                ProfileStatusId = ReadGuid(reader, 9),
                LastUpdated = ReadRowVersion(reader, reader.GetOrdinal("LastUpdated")),
                AffectedSpecies = await reader.NextResultAsync(cancellationToken)
                    ? await ReadAffectedSpeciesAsync(reader, cancellationToken)
                    : []
            };

            return profile;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.GetProfile);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<NewProfileDefaults?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileManagementStoredProcedures.GetProfileVersionInfoById,
                    new { Id = cloneProfileVersionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var scenarioTitle = ReadString(reader, 9);
            var profileStatusId = ReadGuid(reader, 12);

            var title = string.Empty;
            var parentId = Guid.Empty;
            var parentTitle = string.Empty;

            if (isWhatIfScenario)
            {
                // A "what-if" scenario belongs to the same current-situation profile as the
                // cloned version; if the cloned version is not itself a scenario, it belongs
                // to that current-situation profile directly.
                parentId = ReadGuid(reader, 10);
                if (parentId == Guid.Empty)
                {
                    parentId = ReadGuid(reader, 1);
                }

                parentTitle = ReadString(reader, 2);
            }
            else
            {
                title = ReadString(reader, 2);
            }

            return new NewProfileDefaults
            {
                Title = title,
                ScenarioTitle = scenarioTitle,
                ParentId = parentId,
                ParentTitle = parentTitle,
                ProfileStatusId = profileStatusId,
                AffectedSpecies = await reader.NextResultAsync(cancellationToken)
                    ? await ReadAffectedSpeciesAsync(reader, cancellationToken)
                    : []
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.GetProfileVersionInfoById);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AffectedSpeciesInfo?> GetAffectedSpeciesAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileManagementStoredProcedures.GetSpeciesNameById,
                    new { SpeciesId = speciesId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            // spgSpeciesNameById returns only the name and active state; the type is not
            // meaningful outside the context of a specific profile version, exactly as in the
            // legacy ProfileManagementService.GetAffectedSpecies.
            return new AffectedSpeciesInfo
            {
                SpeciesId = speciesId,
                Name = ReadString(reader, 1),
                Type = string.Empty,
                IsActive = ReadBoolean(reader, 2)
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.GetSpeciesNameById);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileStatusType>> GetProfileStatusTypesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ProfileStatusTypeRow>(new CommandDefinition(
                ProfileManagementStoredProcedures.GetProfileStatusTypes,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ProfileStatusType
            {
                Id = row.Id,
                Name = row.Name ?? string.Empty,
                IsValidationComplete = row.IsValidationComplete
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.GetProfileStatusTypes);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.UpdateProfileVersionPublicFlag,
                new { ProfileVersionId = profileVersionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.UpdateProfileVersionPublicFlag);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateProfileStatusAsync(Guid profileId, Guid profileStatusId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.UpdateProfileStatus,
                new { ProfileId = profileId, ProfileStatusId = profileStatusId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileManagementStoredProcedures.UpdateProfileStatus);
            throw;
        }
    }

    private static async Task<byte[]> InsertProfileAsync(
        DbConnection connection,
        DbTransaction transaction,
        CreateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@ProfileId", command.Id, DbType.Guid);
        parameters.Add("@ParentId", command.ParentId != Guid.Empty ? command.ParentId : null, DbType.Guid);
        parameters.Add("@Title", command.Title.Length > 0 ? command.Title : null, DbType.String);
        parameters.Add("@ScenarioTitle", command.ScenarioTitle.Length > 0 ? command.ScenarioTitle : null, DbType.String);
        parameters.Add(
            "@ProfileStatusId",
            command.ProfileStatusId != Guid.Empty ? command.ProfileStatusId : null,
            DbType.Guid);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.InsertProfile,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    private static async Task InsertInitialProfileVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        CreateProfileCommand command,
        CancellationToken cancellationToken)
    {
        // A scenario's initial version title comes from ParentTitle; a current-situation
        // profile's comes from Title. Matches the legacy CreateProfile behaviour exactly.
        var title = command.ParentId == Guid.Empty ? command.Title : command.ParentTitle;

        var parameters = new DynamicParameters();
        parameters.Add("@ProfileVersionId", command.CurrentDraftProfileVersionId, DbType.Guid);
        parameters.Add("@ProfileId", command.Id, DbType.Guid);
        parameters.Add("@Title", title, DbType.String);
        parameters.Add("@ScenarioTitle", command.ScenarioTitle.Length > 0 ? command.ScenarioTitle : null, DbType.String);
        parameters.Add("@VersionMajor", (byte)0, DbType.Byte);
        parameters.Add("@VersionMinor", (byte)1, DbType.Byte);
        parameters.Add("@State", "Draft", DbType.String);
        parameters.Add("@EffectiveDateFrom", DateTime.Now, DbType.DateTime);
        parameters.Add(
            "@CloneProfileVersionId",
            command.CloneProfileVersionId != Guid.Empty ? command.CloneProfileVersionId : null,
            DbType.Guid);
        parameters.Add("@IsPublic", false, DbType.Boolean);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.InsertProfileVersion,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertAffectedSpeciesAsync(
        DbConnection connection,
        DbTransaction transaction,
        AffectedSpeciesInsertDto species,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.InsertProfileVersionSpecies,
            new
            {
                species.ProfileVersionId,
                CloneProfileVersionId = species.CloneProfileVersionId != Guid.Empty ? species.CloneProfileVersionId : (Guid?)null,
                species.SpeciesId,
                AffectedSpeciesTypeName = species.Type
            },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        // Trade/questionnaire data is copied in for a newly added Profiled species, exactly as
        // the legacy CreateProfile/UpdateProfileAttributes InsertAffectedSpecies did - a
        // different, simpler rule than the one NewProfileVersionCommand applies when cloning a
        // whole version forward.
        if (string.Equals(species.Type, ProfiledSpeciesType, StringComparison.Ordinal))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData,
                new { species.ProfileVersionId, species.SpeciesId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task DeleteAffectedSpeciesAsync(
        DbConnection connection,
        DbTransaction transaction,
        AffectedSpeciesDeleteDto species,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.DeleteProfileVersionSpecies,
            new { species.ProfileVersionId, species.SpeciesId },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Reads result set 1 (existence check) and result set 2 (affected species identifiers) of
    /// <c>spgProfileVersionInfoById</c>, exactly as the legacy
    /// <c>DeleteProfileVersion.GetAffectedSpeciesForDeletion</c> did.
    /// </summary>
    /// <returns>The affected species identifiers, or <see langword="null"/> when the version does not exist.</returns>
    private static async Task<List<Guid>?> ReadAffectedSpeciesIdsForDeletionAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileManagementStoredProcedures.GetProfileVersionInfoById,
                new { Id = profileVersionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var speciesIds = new List<Guid>();

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                speciesIds.Add(ReadGuid(reader, 0));
            }
        }

        return speciesIds;
    }

    /// <summary>
    /// Replicates <c>NewProfileVersionCommand.Execute</c>: reads and validates the source
    /// version, then creates the new version, its affected species and (when publishing)
    /// recalculates prioritisation.
    /// </summary>
    private static async Task<Guid> ExecuteNewProfileVersionWorkflowAsync(
        DbConnection connection,
        DbTransaction transaction,
        CreateNewProfileVersionCommand command,
        CancellationToken cancellationToken)
    {
        var currentVersion = await ReadCurrentVersionDetailsAsync(connection, transaction, command.ProfileVersionId, cancellationToken);

        ValidateSourceVersion(command, currentVersion);

        var (title, scenarioTitle, isWhatIfScenario) = await ReadProfileDetailsAsync(
            connection,
            transaction,
            currentVersion.ProfileId,
            currentVersion.ParentProfileId,
            cancellationToken);

        var newProfileVersionId = Guid.NewGuid();
        var effectiveDateFrom = DateTime.Today;
        var (newVersionMajor, newVersionMinor) = command.IsPublished
            ? ((byte)(currentVersion.VersionMajor + 1), (byte)0)
            : (currentVersion.VersionMajor, (byte)(currentVersion.VersionMinor + 1));
        var newState = command.IsPublished ? "Published" : "Draft";

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.UpdateProfileVersionCurrency,
            new { command.ProfileVersionId, State = newState, EffectiveDateTo = effectiveDateFrom },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var createParameters = new DynamicParameters();
        createParameters.Add("@ProfileVersionId", newProfileVersionId, DbType.Guid);
        createParameters.Add("@ProfileId", currentVersion.ProfileId, DbType.Guid);
        createParameters.Add("@Title", title, DbType.String);
        createParameters.Add("@ScenarioTitle", scenarioTitle.Length > 0 ? scenarioTitle : null, DbType.String);
        createParameters.Add("@VersionMajor", newVersionMajor, DbType.Byte);
        createParameters.Add("@VersionMinor", newVersionMinor, DbType.Byte);
        createParameters.Add("@State", newState, DbType.String);
        createParameters.Add("@EffectiveDateFrom", effectiveDateFrom, DbType.DateTime);
        createParameters.Add("@CloneProfileVersionId", command.ProfileVersionId, DbType.Guid);
        createParameters.Add("@IsPublic", command.IsPublic, DbType.Boolean);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileManagementStoredProcedures.InsertProfileVersion,
            createParameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        await CloneAffectedSpeciesAsync(
            connection,
            transaction,
            command.ProfileVersionId,
            newProfileVersionId,
            currentVersion.AffectedSpecies,
            isWhatIfScenario,
            cancellationToken);

        if (command.IsPublished)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.CalculatePrioritisation,
                new { ProfileVersionId = newProfileVersionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.CalculatePrioritisationScore,
                transaction: transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        return newProfileVersionId;
    }

    /// <summary>Mirrors <c>NewProfileVersionCommand.GetCurrentVersionDetails</c>.</summary>
    private static async Task<CurrentVersionDetails> ReadCurrentVersionDetailsAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileManagementStoredProcedures.GetProfileVersionInfoById,
                new { Id = profileVersionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        // No row means the version does not exist; IsLatestVersion defaults to false, which
        // naturally routes into the same "not the latest version" rejection the legacy command
        // fell through to, rather than special-casing not-found here.
        var profileId = Guid.Empty;
        var versionMajor = (byte)0;
        var versionMinor = (byte)0;
        var isPublished = false;
        var isLatestVersion = false;
        var scenarioTitle = string.Empty;
        var parentProfileId = Guid.Empty;

        while (await reader.ReadAsync(cancellationToken))
        {
            profileId = ReadGuid(reader, 1);
            versionMajor = ReadByte(reader, 3);
            versionMinor = ReadByte(reader, 4);
            isPublished = !string.Equals(ReadString(reader, 5), "Draft", StringComparison.Ordinal);
            isLatestVersion = ReadBoolean(reader, 8);
            scenarioTitle = ReadString(reader, 9);
            parentProfileId = ReadGuid(reader, 10);
        }

        var affectedSpecies = new List<AffectedSpeciesInfo>();

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                affectedSpecies.Add(new AffectedSpeciesInfo
                {
                    SpeciesId = ReadGuid(reader, 0),
                    Name = string.Empty,
                    Type = ReadString(reader, 2),
                    IsActive = ReadBoolean(reader, 3)
                });
            }
        }

        return new CurrentVersionDetails(
            profileId,
            versionMajor,
            versionMinor,
            isPublished,
            isLatestVersion,
            scenarioTitle,
            parentProfileId,
            affectedSpecies);
    }

    /// <summary>Mirrors <c>NewProfileVersionCommand.GetProfileDetails</c>.</summary>
    /// <returns>The new version's title, its scenario title, and whether it is a "what-if" scenario.</returns>
    private static async Task<(string Title, string ScenarioTitle, bool IsWhatIfScenario)> ReadProfileDetailsAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileId,
        Guid parentProfileId,
        CancellationToken cancellationToken)
    {
        var isWhatIfScenario = parentProfileId != Guid.Empty;

        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileManagementStoredProcedures.GetProfile,
                new { ProfileId = profileId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        var title = string.Empty;
        var scenarioTitle = string.Empty;

        while (await reader.ReadAsync(cancellationToken))
        {
            title = isWhatIfScenario ? ReadString(reader, 4) : ReadString(reader, 1);
            scenarioTitle = ReadString(reader, 2);
        }

        return (title, scenarioTitle, isWhatIfScenario);
    }

    /// <summary>Mirrors <c>NewProfileVersionCommand.AddAffectedSpecies</c>.</summary>
    private static async Task CloneAffectedSpeciesAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid sourceProfileVersionId,
        Guid newProfileVersionId,
        IReadOnlyList<AffectedSpeciesInfo> affectedSpecies,
        bool isWhatIfScenario,
        CancellationToken cancellationToken)
    {
        foreach (var species in affectedSpecies.Where(species => species.IsActive))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.InsertProfileVersionSpecies,
                new
                {
                    ProfileVersionId = newProfileVersionId,
                    CloneProfileVersionId = sourceProfileVersionId,
                    species.SpeciesId,
                    AffectedSpeciesTypeName = species.Type
                },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        // Trade data is only copied forward for a version that is not itself a "what-if"
        // scenario, exactly as the legacy command's IsWhatIfScenario() guard did.
        if (isWhatIfScenario)
        {
            return;
        }

        foreach (var species in affectedSpecies.Where(species =>
            species.IsActive && string.Equals(species.Type, ProfiledSpeciesType, StringComparison.Ordinal)))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData,
                new { ProfileVersionId = newProfileVersionId, species.SpeciesId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Replicates the business rules enforced by <c>NewProfileVersionCommand.Execute</c>.</summary>
    /// <exception cref="ConcurrencyException">Thrown when any rule is violated.</exception>
    private static void ValidateSourceVersion(CreateNewProfileVersionCommand command, CurrentVersionDetails currentVersion)
    {
        if (!currentVersion.IsLatestVersion)
        {
            throw new ConcurrencyException(
                $"Version {currentVersion.VersionMajor}.{currentVersion.VersionMinor} is not the latest version of " +
                "this profile. The profile may have been altered by another user.");
        }

        if (currentVersion.IsPublished && command.IsPublished)
        {
            throw new ConcurrencyException("You cannot publish a profile based on an already published version.");
        }

        if (command.IsPublic && !command.IsPublished)
        {
            throw new ConcurrencyException("You cannot make a draft profile public.");
        }

        var hasActiveProfiledSpecies = currentVersion.AffectedSpecies.Any(species =>
            species.IsActive && string.Equals(species.Type, ProfiledSpeciesType, StringComparison.Ordinal));

        if (!hasActiveProfiledSpecies)
        {
            throw new ConcurrencyException(
                "You cannot create a new version of this profile as it has no active profiled species.");
        }
    }

    private static async Task<List<AffectedSpeciesInfo>> ReadAffectedSpeciesAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var species = new List<AffectedSpeciesInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            species.Add(new AffectedSpeciesInfo
            {
                SpeciesId = ReadGuid(reader, 0),
                Name = ReadString(reader, 1),
                Type = ReadString(reader, 2),
                IsActive = ReadBoolean(reader, 3)
            });
        }

        return species;
    }

    private static async Task RollbackAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The transaction was already rolled back by the server; nothing left to undo.
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ProfileManagementRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    private static string ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static bool ReadBoolean(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    private static Guid ReadGuid(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);

    private static byte ReadByte(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? (byte)0 : reader.GetByte(ordinal);

    private static byte[] ReadRowVersion(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return [];
        }

        var buffer = new byte[RowVersionLength];
        reader.GetBytes(ordinal, 0, buffer, 0, RowVersionLength);

        return buffer;
    }

    /// <summary>Row shape returned by <c>spgaProfileStatusType</c>; column names map by Dapper convention.</summary>
    private sealed record ProfileStatusTypeRow(Guid Id, string? Name, bool IsValidationComplete);

    /// <summary>Details of the profile version a new version is being created from.</summary>
    private sealed record CurrentVersionDetails(
        Guid ProfileId,
        byte VersionMajor,
        byte VersionMinor,
        bool IsPublished,
        bool IsLatestVersion,
        string ScenarioTitle,
        Guid ParentProfileId,
        IReadOnlyList<AffectedSpeciesInfo> AffectedSpecies);
}
