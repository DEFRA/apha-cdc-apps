using CDC.Api.Domain.Common;
using CDC.Api.Features.CrossCuttingIssueScores.Dtos;
using CDC.Api.Features.CrossCuttingIssueScores.Interfaces;
using MediatR;

namespace CDC.Api.Features.CrossCuttingIssueScores.Queries;

/// <summary>Retrieves every cross-cutting issue with its criteria and scored values.</summary>
public sealed record GetCrossCuttingIssueScoresQuery : IRequest<Result<IReadOnlyList<CrossCuttingIssueCategoryDto>>>;

/// <summary>Handles <see cref="GetCrossCuttingIssueScoresQuery"/>.</summary>
/// <param name="repository">Cross-cutting issue scores data access.</param>
public sealed class GetCrossCuttingIssueScoresQueryHandler(ICrossCuttingIssueScoresRepository repository)
    : IRequestHandler<GetCrossCuttingIssueScoresQuery, Result<IReadOnlyList<CrossCuttingIssueCategoryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every issue with its criteria and values.</returns>
    public async Task<Result<IReadOnlyList<CrossCuttingIssueCategoryDto>>> Handle(
        GetCrossCuttingIssueScoresQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await repository.GetCategoriesAsync(cancellationToken));
}
