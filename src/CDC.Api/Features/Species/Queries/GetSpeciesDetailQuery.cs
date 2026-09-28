using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves the name/parent detail of one species, for the "Edit name/parent" screen.
/// </summary>
/// <param name="SpeciesId">The species to read.</param>
public sealed record GetSpeciesDetailQuery(Guid SpeciesId) : IRequest<Result<SpeciesDetailDto>>;

/// <summary>
/// Handles <see cref="GetSpeciesDetailQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetSpeciesDetailQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetSpeciesDetailQuery, Result<SpeciesDetailDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species detail, or a not-found result when the species does not exist.</returns>
    public async Task<Result<SpeciesDetailDto>> Handle(GetSpeciesDetailQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await speciesService.GetSpeciesDetailAsync(request.SpeciesId, cancellationToken);

        return detail is null
            ? Result.NotFound<SpeciesDetailDto>($"Species '{request.SpeciesId}' was not found.")
            : Result.Success(detail);
    }
}
