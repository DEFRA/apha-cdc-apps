using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>
/// Recalculates the prioritisation scores of published profiles after the global prioritisation variables change -
/// the <c>sppPrioritisationScore</c> call the legacy <c>UpdateProfilePrioritisationMetadata</c> made after saving.
/// </summary>
public sealed record RecalculatePrioritisationScoresCommand : IRequest<Result<Unit>>;

/// <summary>
/// Handles <see cref="RecalculatePrioritisationScoresCommand"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class RecalculatePrioritisationScoresCommandHandler(
    IPrioritisationVariablesRepository repository,
    ILogger<RecalculatePrioritisationScoresCommandHandler> logger)
    : IRequestHandler<RecalculatePrioritisationScoresCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the scores have been recalculated.</returns>
    public async Task<Result<Unit>> Handle(RecalculatePrioritisationScoresCommand request, CancellationToken cancellationToken)
    {
        await repository.RecalculatePrioritisationScoresAsync(cancellationToken);
        logger.PrioritisationScoresRecalculated();

        return Result.Success(Unit.Value);
    }
}
