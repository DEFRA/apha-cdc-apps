using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves the species questionnaire structure.
/// </summary>
public sealed record GetSpeciesMetadataQuery : IRequest<Result<SpeciesMetadataDto>>;

/// <summary>
/// Handles <see cref="GetSpeciesMetadataQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetSpeciesMetadataQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetSpeciesMetadataQuery, Result<SpeciesMetadataDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Sections, questions and fields.</returns>
    public async Task<Result<SpeciesMetadataDto>> Handle(GetSpeciesMetadataQuery request, CancellationToken cancellationToken)
    {
        var metadata = await speciesService.GetSpeciesMetadataAsync(cancellationToken);

        return Result.Success(metadata);
    }
}
