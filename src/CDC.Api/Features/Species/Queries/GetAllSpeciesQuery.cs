using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves every species and species group.
/// </summary>
public sealed record GetAllSpeciesQuery : IRequest<Result<IReadOnlyList<SpeciesDto>>>;

/// <summary>
/// Handles <see cref="GetAllSpeciesQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetAllSpeciesQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetAllSpeciesQuery, Result<IReadOnlyList<SpeciesDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The full species list.</returns>
    public async Task<Result<IReadOnlyList<SpeciesDto>>> Handle(GetAllSpeciesQuery request, CancellationToken cancellationToken)
    {
        var species = await speciesService.GetAllSpeciesAsync(cancellationToken);

        return Result.Success(species);
    }
}
