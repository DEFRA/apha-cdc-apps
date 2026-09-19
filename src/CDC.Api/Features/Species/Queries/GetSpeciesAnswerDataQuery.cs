using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves the stored answers for one species.
/// </summary>
/// <param name="SpeciesId">The species to read.</param>
public sealed record GetSpeciesAnswerDataQuery(Guid SpeciesId) : IRequest<Result<SpeciesAnswerDataDto>>;

/// <summary>
/// Handles <see cref="GetSpeciesAnswerDataQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetSpeciesAnswerDataQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetSpeciesAnswerDataQuery, Result<SpeciesAnswerDataDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The answer data, or a not-found result when the species does not exist.</returns>
    public async Task<Result<SpeciesAnswerDataDto>> Handle(GetSpeciesAnswerDataQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var answerData = await speciesService.GetSpeciesAnswerDataAsync(request.SpeciesId, cancellationToken);

        return answerData is null
            ? Result.NotFound<SpeciesAnswerDataDto>($"Species '{request.SpeciesId}' was not found.")
            : Result.Success(answerData);
    }
}
