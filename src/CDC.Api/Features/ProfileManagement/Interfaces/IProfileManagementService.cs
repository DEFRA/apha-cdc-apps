using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;

namespace CDC.Api.Features.ProfileManagement.Interfaces;

/// <summary>
/// Application service for the profile management feature. Owns the mapping between domain
/// entities and the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface IProfileManagementService
{
    /// <summary>Creates a profile, its initial draft version and any affected species.</summary>
    /// <param name="command">The profile to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new profile's identifier and row version.</returns>
    Task<CreateProfileResultDto> CreateProfileAsync(CreateProfileCommand command, CancellationToken cancellationToken);

    /// <summary>Updates a profile's attributes and applies affected species changes.</summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new row version.</returns>
    Task<UpdateProfileAttributesResultDto> UpdateProfileAttributesAsync(
        UpdateProfileAttributesCommand command,
        CancellationToken cancellationToken);

    /// <summary>Deletes a profile version and its affected species.</summary>
    /// <param name="profileVersionId">The profile version to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Which version is now latest, or <see langword="null"/> when it does not exist.</returns>
    Task<DeleteProfileVersionResultDto?> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Creates a new version of a profile.</summary>
    /// <param name="command">The profile version to base the new version on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new profile version's identifier.</returns>
    Task<NewProfileVersionResultDto> CreateNewProfileVersionAsync(
        CreateNewProfileVersionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets a profile's attributes, current version pointers and affected species.</summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile's attributes, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>Gets default values for a new profile, sourced from the version being cloned.</summary>
    /// <param name="cloneProfileVersionId">The profile version to read defaults from.</param>
    /// <param name="isWhatIfScenario">Whether the new profile will be a "what-if" scenario.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The default values, or <see langword="null"/> when the source version does not exist.</returns>
    Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken);

    /// <summary>Gets one species' name and active state.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species, or <see langword="null"/> when it does not exist.</returns>
    Task<AffectedSpeciesDto?> GetAffectedSpeciesAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Gets every profile status a profile can be set to.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every profile status.</returns>
    Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken);

    /// <summary>Toggles a profile version's public visibility flag.</summary>
    /// <param name="profileVersionId">The profile version to toggle.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Sets a profile's status.</summary>
    /// <param name="profileId">The profile to update.</param>
    /// <param name="profileStatusId">The status to set.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task UpdateProfileStatusAsync(Guid profileId, Guid profileStatusId, CancellationToken cancellationToken);
}
