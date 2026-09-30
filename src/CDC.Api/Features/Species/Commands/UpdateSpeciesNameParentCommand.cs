using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Updates the name and/or parent of one species, transactionally with an audit trail entry,
/// and returns the new row version.
/// </summary>
public sealed record UpdateSpeciesNameParentCommand : IRequest<Result<UpdateSpeciesNameParentResultDto>>
{
    /// <summary>Gets the species being updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the new display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the new parent identifier; <see cref="Guid.Empty"/> for a root species.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Recorded in the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the id of the <c>[User]</c> row recorded as the author of the change in the audit
    /// trail. Set by the controller, never taken from client input.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the row version last read for this species. The update is rejected with HTTP 409
    /// if it no longer matches the stored value.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
