using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Commands;

namespace CDC.Api.Features.ProfileManagement.Interfaces;

/// <summary>
/// Data access for profiles, profile versions and their affected species. Every member maps
/// onto the stored procedures the legacy <c>Profiles.DataAccess.Sql.ProfileManagementService</c>
/// and <c>NewProfileVersionCommand</c> used, so behaviour is preserved. State-dependent business
/// rules that must be checked atomically alongside a write (for example, "is this the latest
/// version?") are necessarily evaluated here, within the same transaction as the write itself.
/// </summary>
public interface IProfileManagementRepository
{
    /// <summary>
    /// Creates a profile, its initial draft version and any affected species, in one
    /// transaction, via <c>spiProfile</c>, <c>spiProfileVersion</c>, <c>spiProfileVersionSpecies</c>
    /// and (for <c>Profiled</c> species) <c>spuProfileVersionSpeciesTradeData</c>.
    /// </summary>
    /// <param name="command">The profile to create.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new profile's identifier and row version.</returns>
    Task<ProfileCreationResult> CreateProfileAsync(CreateProfileCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a profile's title/scenario title and applies affected species changes, in one
    /// transaction, via <c>spuProfile</c>, <c>spdProfileVersionSpecies</c> and
    /// <c>spiProfileVersionSpecies</c>.
    /// </summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new row version.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when <see cref="UpdateProfileAttributesCommand.LastUpdated"/> no longer matches
    /// the stored row version.
    /// </exception>
    Task<byte[]> UpdateProfileAttributesAsync(UpdateProfileAttributesCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a profile version and its affected species, in one transaction, via
    /// <c>spgProfileVersionInfoById</c>, <c>spdProfileVersionSpecies</c> and <c>spdProfileVersion</c>.
    /// </summary>
    /// <param name="profileVersionId">The profile version to delete.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Which version is now latest, or <see langword="null"/> when it does not exist.</returns>
    Task<DeleteProfileVersionResult?> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Replicates <c>NewProfileVersionCommand.vb</c>: reads the source version
    /// (<c>spgProfileVersionInfoById</c>), validates it is the latest version and the
    /// publish/public/active-species rules, updates its effective date
    /// (<c>sppProfileVersionCurrency</c>), creates the new version (<c>spiProfileVersion</c>),
    /// clones its active affected species and trade data (<c>spiProfileVersionSpecies</c>,
    /// <c>spuProfileVersionSpeciesTradeData</c>) and, when publishing, recalculates
    /// prioritisation (<c>sppPrioritisationCalculation</c>, <c>sppPrioritisationScore</c>). All
    /// in one transaction.
    /// </summary>
    /// <param name="command">The profile version to base the new version on.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new profile version's identifier.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the source version is not latest, the publish/public rules are violated, or
    /// it has no active profiled species.
    /// </exception>
    Task<Guid> CreateNewProfileVersionAsync(CreateNewProfileVersionCommand command, CancellationToken cancellationToken);

    /// <summary>Reads a profile's attributes and affected species via <c>spgProfile</c>.</summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The profile, or <see langword="null"/> when it does not exist.</returns>
    Task<Profile?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads default values for a new profile from the version being cloned, via
    /// <c>spgProfileVersionInfoById</c>.
    /// </summary>
    /// <param name="cloneProfileVersionId">The profile version to read defaults from.</param>
    /// <param name="isWhatIfScenario">Whether the new profile will be a "what-if" scenario.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The default values, or <see langword="null"/> when the source version does not exist.</returns>
    Task<NewProfileDefaults?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken);

    /// <summary>Reads one species' name and active state via <c>spgSpeciesNameById</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The species, or <see langword="null"/> when it does not exist.</returns>
    Task<AffectedSpeciesInfo?> GetAffectedSpeciesAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Reads every profile status via <c>spgaProfileStatusType</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every profile status.</returns>
    Task<IReadOnlyList<ProfileStatusType>> GetProfileStatusTypesAsync(CancellationToken cancellationToken);

    /// <summary>Toggles a profile version's public flag via <c>spuProfileVersionPublicFlag</c>.</summary>
    /// <param name="profileVersionId">The profile version to toggle.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Sets a profile's status via <c>spuProfileStatus</c>.</summary>
    /// <param name="profileId">The profile to update.</param>
    /// <param name="profileStatusId">The status to set.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateProfileStatusAsync(Guid profileId, Guid profileStatusId, CancellationToken cancellationToken);
}
