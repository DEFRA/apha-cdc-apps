using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Handles <see cref="InactivateSpeciesCommand"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class InactivateSpeciesCommandHandler(
    ISpeciesService speciesService,
    ILogger<InactivateSpeciesCommandHandler> logger)
    : IRequestHandler<InactivateSpeciesCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The species to inactivate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the species is inactivated, or a conflict result when another user has saved first.</returns>
    public async Task<Result<Unit>> Handle(InactivateSpeciesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await speciesService.InactivateSpeciesAsync(request, cancellationToken);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            // An expected outcome rather than a fault: the client should re-read and retry,
            // so it is reported as a 409 result instead of bubbling up to the middleware.
            logger.ConcurrencyConflict(request.SpeciesId);

            return Result.Conflict<Unit>(exception.Message);
        }
    }
}
