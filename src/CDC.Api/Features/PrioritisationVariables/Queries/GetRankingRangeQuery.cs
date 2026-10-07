using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Queries;

/// <summary>
/// Retrieves the ranking range profile prioritisation scores are normalised into.
/// </summary>
public sealed record GetRankingRangeQuery : IRequest<Result<PrioritisationRankingRangeDto>>;

/// <summary>
/// Handles <see cref="GetRankingRangeQuery"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
public sealed class GetRankingRangeQueryHandler(IPrioritisationVariablesRepository repository)
    : IRequestHandler<GetRankingRangeQuery, Result<PrioritisationRankingRangeDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current ranking range.</returns>
    public async Task<Result<PrioritisationRankingRangeDto>> Handle(GetRankingRangeQuery request, CancellationToken cancellationToken)
    {
        var rankingRange = await repository.GetRankingRangeAsync(cancellationToken);

        return Result.Success(new PrioritisationRankingRangeDto
        {
            LowerBound = rankingRange.LowerBound,
            UpperBound = rankingRange.UpperBound,
            RowVersion = Convert.ToBase64String(rankingRange.RowVersion)
        });
    }
}
