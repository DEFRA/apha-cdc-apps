using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Handles <see cref="DeleteSpeciesCommand"/>. Enforces the business rules the legacy CSLA
/// <c>SpeciesDataChange</c> business object checked before deleting: the species must be
/// active, not referenced by a current profile, and have no children.
/// </summary>
/// <param name="speciesService">Species application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class DeleteSpeciesCommandHandler(
    ISpeciesService speciesService,
    ILogger<DeleteSpeciesCommandHandler> logger)
    : IRequestHandler<DeleteSpeciesCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The species to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the species is deleted, or a failure result describing why it could not be.</returns>
    public async Task<Result<Unit>> Handle(DeleteSpeciesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await speciesService.GetSpeciesDetailAsync(request.SpeciesId, cancellationToken);

        if (detail is null)
        {
            return Result.NotFound<Unit>("No species exists with the supplied identifier.");
        }

        if (!detail.IsActive)
        {
            return Result.ValidationFailed<Unit>("You cannot delete a species that is inactive");
        }

        if (detail.IsInUse)
        {
            return Result.ValidationFailed<Unit>("You cannot delete a species that is used within a current profile.");
        }

        if (detail.ChildCount > 0)
        {
            return Result.ValidationFailed<Unit>("You cannot delete a species that has children");
        }

        try
        {
            await speciesService.DeleteSpeciesAsync(request, cancellationToken);

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
