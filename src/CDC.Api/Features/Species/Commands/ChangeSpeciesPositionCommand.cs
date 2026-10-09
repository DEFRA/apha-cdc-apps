using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Moves a species or species group up or down within its parent's sequence, swapping its
/// sequence number with the sibling in that direction. Writes an audit trail entry recording
/// who made the move and when.
/// </summary>
/// <param name="SpeciesId">The species to move.</param>
/// <param name="IsMovingUp">True to swap with the previous sibling; false for the next.</param>
/// <param name="UserId">
/// The id of the <c>[User]</c> row recorded as the author of the change in the audit trail. Set
/// by the controller, never taken from client input.
/// </param>
public sealed record ChangeSpeciesPositionCommand(Guid SpeciesId, bool IsMovingUp, Guid UserId) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="ChangeSpeciesPositionCommand"/>.</summary>
/// <param name="speciesService">Species application service.</param>
public sealed class ChangeSpeciesPositionCommandHandler(ISpeciesService speciesService)
    : IRequestHandler<ChangeSpeciesPositionCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The move to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the sequence numbers are swapped.</returns>
    public async Task<Result<Unit>> Handle(ChangeSpeciesPositionCommand request, CancellationToken cancellationToken)
    {
        await speciesService.ChangeSpeciesPositionAsync(request.SpeciesId, request.IsMovingUp, request.UserId, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
