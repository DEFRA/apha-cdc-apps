using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Dtos;

namespace CDC.Api.Features.Species.Interfaces;

/// <summary>
/// Application service for the species feature. Owns the mapping between domain entities and
/// the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface ISpeciesService
{
    /// <summary>Gets every species and species group.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The full species list.</returns>
    Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken);

    /// <summary>Gets the species selected for a disease filter.</summary>
    /// <param name="diseaseName">The disease name to filter by.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching species; empty when the disease name is unknown.</returns>
    Task<IReadOnlyList<SelectedSpeciesDto>> GetAllSelectedSpeciesAsync(string diseaseName, CancellationToken cancellationToken);

    /// <summary>Gets the species questionnaire structure.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Sections, questions and fields.</returns>
    Task<SpeciesMetadataDto> GetSpeciesMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Gets the stored answers for one species.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The answer data, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesAnswerDataDto?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Applies answer changes to one species.</summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species identifier and its new row version.</returns>
    Task<UpdateSpeciesAnswerDataResultDto> UpdateSpeciesAnswerDataAsync(UpdateSpeciesAnswerDataCommand command, CancellationToken cancellationToken);

    /// <summary>Gets the name/parent detail of one species, for the "Edit name/parent" screen.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species detail, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Gets the species that are a legal parent choice for another species.</summary>
    /// <param name="speciesId">The species being re-parented.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The valid parent list.</returns>
    Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken);

    /// <summary>Updates the name and/or parent of one species, with an audit trail entry.</summary>
    /// <param name="command">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species identifier and its new row version.</returns>
    Task<UpdateSpeciesNameParentResultDto> UpdateSpeciesNameParentAsync(UpdateSpeciesNameParentCommand command, CancellationToken cancellationToken);

    /// <summary>Adds a new species or species group, with an audit trail entry.</summary>
    /// <param name="command">The species to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifier assigned to the new species.</returns>
    Task<AddSpeciesResultDto> AddSpeciesAsync(AddSpeciesCommand command, CancellationToken cancellationToken);

    /// <summary>Gets every recorded species name/parent change, most recent first.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The audit trail.</returns>
    Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken);

    /// <summary>Moves a species up or down within its parent's sequence, with an audit trail entry.</summary>
    /// <param name="speciesId">The species to move.</param>
    /// <param name="isMovingUp">True to swap with the previous sibling; false for the next.</param>
    /// <param name="userId">The id of the <c>[User]</c> row recorded as the author of the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ChangeSpeciesPositionAsync(Guid speciesId, bool isMovingUp, Guid userId, CancellationToken cancellationToken);
}
