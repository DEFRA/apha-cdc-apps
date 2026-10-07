using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>
/// Updates the ranking range profile prioritisation scores are normalised into.
/// </summary>
/// <param name="LowerBound">The new lower bound.</param>
/// <param name="UpperBound">The new upper bound.</param>
/// <param name="RowVersion">The concurrency token read alongside the values being replaced, base64-encoded.</param>
public sealed record UpdateRankingRangeCommand(int LowerBound, int UpperBound, string RowVersion)
    : IRequest<Result<PrioritisationRankingRangeDto>>;

/// <summary>
/// Handles <see cref="UpdateRankingRangeCommand"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateRankingRangeCommandHandler(
    IPrioritisationVariablesRepository repository,
    ILogger<UpdateRankingRangeCommandHandler> logger)
    : IRequestHandler<UpdateRankingRangeCommand, Result<PrioritisationRankingRangeDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved range with its new row version, or a conflict result when another user has saved first.</returns>
    public async Task<Result<PrioritisationRankingRangeDto>> Handle(UpdateRankingRangeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var newRowVersion = await repository.UpdateRankingRangeAsync(
                request.LowerBound,
                request.UpperBound,
                Convert.FromBase64String(request.RowVersion),
                cancellationToken);

            return Result.Success(new PrioritisationRankingRangeDto
            {
                LowerBound = request.LowerBound,
                UpperBound = request.UpperBound,
                RowVersion = Convert.ToBase64String(newRowVersion)
            });
        }
        catch (ConcurrencyException exception)
        {
            // An expected outcome rather than a fault: the client should re-read and retry,
            // so it is reported as a 409 result instead of bubbling up to the middleware.
            logger.RankingRangeConcurrencyConflict();

            return Result.Conflict<PrioritisationRankingRangeDto>(exception.Message);
        }
    }
}
