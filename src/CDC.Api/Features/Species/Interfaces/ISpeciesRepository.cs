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
}
