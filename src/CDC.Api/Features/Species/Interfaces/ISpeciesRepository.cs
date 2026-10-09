using CDC.Api.Domain.Entities;
using CDC.Api.Features.Species.Commands;

namespace CDC.Api.Features.Species.Interfaces;

/// <summary>
/// Data access for species, their questionnaire metadata, and their recorded answers. Every
/// member maps onto the stored procedures the legacy <c>Profiles.DataAccess.Sql</c> layer used,
/// so behaviour is preserved. Implementations must contain no business logic.
/// </summary>
public interface ISpeciesRepository
{
    /// <summary>Reads every species and species group via <c>spgaSpecies</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The full species list, in sequence order.</returns>
    Task<IReadOnlyList<Domain.Entities.Species>> GetAllSpeciesAsync(CancellationToken cancellationToken);

    /// <summary>Reads the species selected for a disease filter via <c>spgaSelectedDiseaseSpecies</c>.</summary>
    /// <param name="diseaseName">The disease name to filter by.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching species; empty when the disease name is unknown.</returns>
    Task<IReadOnlyList<SelectedSpecies>> GetAllSelectedSpeciesAsync(string diseaseName, CancellationToken cancellationToken);

    /// <summary>Reads the questionnaire structure via <c>spgaSpeciesSectionMetadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Sections, questions and fields assembled into a hierarchy.</returns>
    Task<SpeciesMetadata> GetSpeciesMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Reads the stored answers for one species via <c>spgSpeciesAnswerData</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The answer data, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesAnswerData?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies answer changes in a single transaction: the row version check
    /// (<c>spuSpeciesAnswerData</c>), each field update (<c>spuSpeciesFieldValue</c> or the
    /// multi-value delete/insert pair), then the prioritisation score recalculation
    /// (<c>sppSpeciesPrioritisationScore</c>).
    /// </summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new row version of the species record.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task<byte[]> UpdateSpeciesAnswerDataAsync(UpdateSpeciesAnswerDataCommand command, CancellationToken cancellationToken);

    /// <summary>Reads one species' name/parent detail via <c>spgSpeciesById</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The species detail, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesDetail?> GetSpeciesByIdAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Reads the legal parent choices for a species via <c>spgSpeciesValidParents</c>.</summary>
    /// <param name="speciesId">The species being re-parented.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The valid parent list.</returns>
    Task<IReadOnlyList<SpeciesValidParent>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a species' name and parent via <c>spuSpecies</c>, which also writes the audit
    /// trail entry, and returns the new row version.
    /// </summary>
    /// <param name="command">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new row version of the species record.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task<byte[]> UpdateSpeciesNameParentAsync(UpdateSpeciesNameParentCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a new species via <c>spiSpecies</c>, which also allocates its sequence number
    /// and writes the audit trail entry recording <c>- new entry -</c> as the old name and
    /// old parent.
    /// </summary>
    /// <param name="command">The species to add.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The identifier assigned to the new species.</returns>
    /// <exception cref="Domain.Exceptions.DuplicateSpeciesNameException">
    /// Thrown when another species already uses the supplied name.
    /// </exception>
    Task<Guid> AddSpeciesAsync(AddSpeciesCommand command, CancellationToken cancellationToken);

    /// <summary>Reads every recorded species name/parent change via <c>spgaSpeciesTableAuditLog</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The audit trail, most recent entry first.</returns>
    Task<IReadOnlyList<SpeciesAuditTrailEntry>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Swaps a species' sequence number with its previous or next sibling via
    /// <c>spuSpeciesSequenceNumber</c>, which also writes an audit trail entry. A no-op if the
    /// species is already first/last under its parent - the caller is expected to only offer
    /// the move when a sibling exists.
    /// </summary>
    /// <param name="speciesId">The species to move.</param>
    /// <param name="isMovingUp">True to swap with the previous sibling; false for the next.</param>
    /// <param name="userId">The id of the <c>[User]</c> row recorded as the author of the change.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task ChangeSpeciesPositionAsync(Guid speciesId, bool isMovingUp, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a species inactive via <c>sppSpecies</c>, which also writes the audit trail entry.
    /// The species row, its answer data and its hierarchy relationships are retained unchanged.
    /// </summary>
    /// <param name="command">The species to inactivate.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task InactivateSpeciesAsync(InactivateSpeciesCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a species via <c>spdSpecies</c>, which also writes the audit trail entry,
    /// decrements later siblings' sequence numbers, and removes the species' field values and
    /// prioritisation scores. Performs no business rule checks itself - the caller must ensure
    /// the species is active, not in use, and has no children before calling this.
    /// </summary>
    /// <param name="command">The species to delete.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task DeleteSpeciesAsync(DeleteSpeciesCommand command, CancellationToken cancellationToken);
}
