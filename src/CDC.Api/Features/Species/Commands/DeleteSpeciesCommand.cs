using CDC.Api.Domain.Common;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Deletes a species or species group, transactionally with an audit trail entry. The caller
/// (<see cref="DeleteSpeciesCommandHandler"/>) is responsible for enforcing that the species is
/// active, not referenced by a current profile, and has no children before this is sent - the
/// legacy <c>spdSpecies</c> procedure itself performs no such checks.
/// </summary>
public sealed record DeleteSpeciesCommand : IRequest<Result<Unit>>
{
    /// <summary>Gets the species being deleted.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the reason given for the change. Recorded in the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the id of the <c>[User]</c> row recorded as the author of the change in the audit
    /// trail. Set by the controller, never taken from client input.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the row version last read for this species. The delete is rejected with HTTP 409
    /// if it no longer matches the stored value.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
