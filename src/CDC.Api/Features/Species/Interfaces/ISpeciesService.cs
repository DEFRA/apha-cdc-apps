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
}
