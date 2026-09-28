using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Features.Species.Mapping;

namespace CDC.Api.Features.Species;

/// <summary>
/// Default <see cref="ISpeciesService"/>: reads through <see cref="ISpeciesRepository"/> and
/// maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Species data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SpeciesService(ISpeciesRepository repository, ILogger<SpeciesService> logger) : ISpeciesService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken)
    {
        var species = await repository.GetAllSpeciesAsync(cancellationToken);
        logger.RetrievedAllSpecies(species.Count);

        return [.. species.Select(item => item.ToDto())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SelectedSpeciesDto>> GetAllSelectedSpeciesAsync(string diseaseName, CancellationToken cancellationToken)
    {
        var species = await repository.GetAllSelectedSpeciesAsync(diseaseName, cancellationToken);
        logger.RetrievedSelectedSpecies(species.Count, diseaseName);

        return [.. species.Select(item => item.ToDto())];
    }

    /// <inheritdoc />
    public async Task<SpeciesMetadataDto> GetSpeciesMetadataAsync(CancellationToken cancellationToken)
    {
        var metadata = await repository.GetSpeciesMetadataAsync(cancellationToken);
        logger.RetrievedSpeciesMetadata(metadata.Sections.Count);

        return metadata.ToDto();
    }

    /// <inheritdoc />
    public async Task<SpeciesAnswerDataDto?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        var answerData = await repository.GetSpeciesAnswerDataAsync(speciesId, cancellationToken);

        if (answerData is null)
        {
            logger.SpeciesAnswerDataNotFound(speciesId);
            return null;
        }

        logger.RetrievedSpeciesAnswerData(speciesId, answerData.Sections.Count);

        return answerData.ToDto();
    }

    /// <inheritdoc />
    public async Task<UpdateSpeciesAnswerDataResultDto> UpdateSpeciesAnswerDataAsync(
        UpdateSpeciesAnswerDataCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.UpdatingSpeciesAnswerData(command.Changes.Count, command.SpeciesId);

        var lastUpdated = await repository.UpdateSpeciesAnswerDataAsync(command, cancellationToken);

        logger.UpdatedSpeciesAnswerData(command.SpeciesId);

        return new UpdateSpeciesAnswerDataResultDto
        {
            SpeciesId = command.SpeciesId,
            LastUpdated = lastUpdated
        };
    }

    /// <inheritdoc />
    public async Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        var detail = await repository.GetSpeciesByIdAsync(speciesId, cancellationToken);

        if (detail is null)
        {
            logger.SpeciesDetailNotFound(speciesId);
            return null;
        }

        logger.RetrievedSpeciesDetail(speciesId);

        return detail.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        var validParents = await repository.GetSpeciesValidParentsAsync(speciesId, cancellationToken);
        logger.RetrievedSpeciesValidParents(validParents.Count, speciesId);

        return [.. validParents.Select(item => item.ToDto())];
    }

    /// <inheritdoc />
    public async Task<UpdateSpeciesNameParentResultDto> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.UpdatingSpeciesNameParent(command.SpeciesId);

        var lastUpdated = await repository.UpdateSpeciesNameParentAsync(command, cancellationToken);

        logger.UpdatedSpeciesNameParent(command.SpeciesId);

        return new UpdateSpeciesNameParentResultDto
        {
            SpeciesId = command.SpeciesId,
            LastUpdated = lastUpdated
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken)
    {
        var entries = await repository.GetSpeciesAuditTrailAsync(cancellationToken);
        logger.RetrievedSpeciesAuditTrail(entries.Count);

        return [.. entries.Select(item => item.ToDto())];
    }
}
