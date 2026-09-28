using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves the species that are a legal parent choice for another species (itself and its
/// own descendants are excluded, so the hierarchy cannot form a cycle).
/// </summary>
/// <param name="SpeciesId">The species being re-parented.</param>
public sealed record GetSpeciesValidParentsQuery(Guid SpeciesId) : IRequest<Result<IReadOnlyList<SpeciesValidParentDto>>>;

/// <summary>
/// Handles <see cref="GetSpeciesValidParentsQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetSpeciesValidParentsQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetSpeciesValidParentsQuery, Result<IReadOnlyList<SpeciesValidParentDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The valid parent list.</returns>
    public async Task<Result<IReadOnlyList<SpeciesValidParentDto>>> Handle(
        GetSpeciesValidParentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validParents = await speciesService.GetSpeciesValidParentsAsync(request.SpeciesId, cancellationToken);

        return Result.Success(validParents);
    }
}
