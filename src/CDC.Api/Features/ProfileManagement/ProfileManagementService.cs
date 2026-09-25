using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileManagement.Mapping;

namespace CDC.Api.Features.ProfileManagement;

/// <summary>
/// Default <see cref="IProfileManagementService"/>: reads and writes through
/// <see cref="IProfileManagementRepository"/> and maps domain entities onto the DTOs the API
/// returns.
/// </summary>
/// <param name="repository">Profile management data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileManagementService(IProfileManagementRepository repository, ILogger<ProfileManagementService> logger)
    : IProfileManagementService
{
    /// <inheritdoc />
    public async Task<CreateProfileResultDto> CreateProfileAsync(CreateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await repository.CreateProfileAsync(command, cancellationToken);
        logger.CreatedProfile(result.NewProfileId);

        return result.ToDto();
    }

    /// <inheritdoc />
    public async Task<UpdateProfileAttributesResultDto> UpdateProfileAttributesAsync(
        UpdateProfileAttributesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newLastUpdated = await repository.UpdateProfileAttributesAsync(command, cancellationToken);
        logger.UpdatedProfileAttributes(command.Id);

        return new UpdateProfileAttributesResultDto { NewLastUpdated = newLastUpdated };
    }

    /// <inheritdoc />
    public async Task<DeleteProfileVersionResultDto?> DeleteProfileVersionAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteProfileVersionAsync(profileVersionId, cancellationToken);

        if (result is null)
        {
            logger.ProfileVersionNotFoundForDeletion(profileVersionId);
            return null;
        }

        logger.DeletedProfileVersion(profileVersionId);

        return result.ToDto();
    }

    /// <inheritdoc />
    public async Task<NewProfileVersionResultDto> CreateNewProfileVersionAsync(
        CreateNewProfileVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newProfileVersionId = await repository.CreateNewProfileVersionAsync(command, cancellationToken);
        logger.CreatedNewProfileVersion(newProfileVersionId, command.ProfileVersionId);

        return new NewProfileVersionResultDto { NewProfileVersionId = newProfileVersionId };
    }

    /// <inheritdoc />
    public async Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await repository.GetProfileAttributesAsync(profileId, cancellationToken);

        if (profile is null)
        {
            logger.ProfileNotFound(profileId);
            return null;
        }

        logger.RetrievedProfileAttributes(profileId);

        return profile.ToDto();
    }

    /// <inheritdoc />
    public async Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken)
    {
        var defaults = await repository.GetNewProfileDefaultsAsync(cloneProfileVersionId, isWhatIfScenario, cancellationToken);

        if (defaults is null)
        {
            return null;
        }

        logger.RetrievedNewProfileDefaults(cloneProfileVersionId);

        return defaults.ToDto();
    }

    /// <inheritdoc />
    public async Task<AffectedSpeciesDto?> GetAffectedSpeciesAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        var species = await repository.GetAffectedSpeciesAsync(speciesId, cancellationToken);

        return species?.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken)
    {
        var statusTypes = await repository.GetProfileStatusTypesAsync(cancellationToken);
        logger.RetrievedProfileStatusTypes(statusTypes.Count);

        return [.. statusTypes.Select(status => status.ToDto())];
    }

    /// <inheritdoc />
    public async Task SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await repository.SetProfileVersionPublicAccessAsync(profileVersionId, cancellationToken);
        logger.TogglePublicAccess(profileVersionId);
    }

    /// <inheritdoc />
    public async Task UpdateProfileStatusAsync(Guid profileId, Guid profileStatusId, CancellationToken cancellationToken)
    {
        await repository.UpdateProfileStatusAsync(profileId, profileStatusId, cancellationToken);
        logger.UpdatedProfileStatus(profileId, profileStatusId);
    }
}
