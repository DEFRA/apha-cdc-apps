using CDC.Api.Domain.Common;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Marks a species or species group inactive, transactionally with an audit trail entry. The
/// species row, its answer data and its hierarchy relationships are retained unchanged.
/// </summary>
public sealed record InactivateSpeciesCommand : IRequest<Result<Unit>>
{
    /// <summary>Gets the species being inactivated.</summary>
    public Guid SpeciesId { get; init; }

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
