using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>
/// Calculates the prioritisation scores of every current draft profile version - the legacy "Draft prioritisation"
/// Go button (<c>PrioritiseProfileCommand.PrioritiseProfiles(ProfileVersionStatus.Draft)</c>).
/// </summary>
public sealed record PrioritiseDraftProfilesCommand : IRequest<Result<Unit>>;

/// <summary>
/// Handles <see cref="PrioritiseDraftProfilesCommand"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class PrioritiseDraftProfilesCommandHandler(
    IPrioritisationVariablesRepository repository,
    ILogger<PrioritiseDraftProfilesCommandHandler> logger)
    : IRequestHandler<PrioritiseDraftProfilesCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once every draft profile version has been recalculated.</returns>
    public async Task<Result<Unit>> Handle(PrioritiseDraftProfilesCommand request, CancellationToken cancellationToken)
    {
        var count = await repository.PrioritiseDraftProfilesAsync(cancellationToken);
        logger.DraftProfilesPrioritised(count);

        return Result.Success(Unit.Value);
    }
}
