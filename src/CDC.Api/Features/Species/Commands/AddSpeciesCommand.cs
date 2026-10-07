using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Adds a new species or species group to the hierarchy, transactionally with an audit trail
/// entry, and returns the identifier of the created record.
/// </summary>
public sealed record AddSpeciesCommand : IRequest<Result<AddSpeciesResultDto>>
{
    /// <summary>Gets the display name of the new species.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the chosen parent. <see langword="null"/> means no choice was made and is
    /// rejected; <see cref="Guid.Empty"/> means the new species sits at the root.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Recorded in the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the id of the <c>[User]</c> row recorded as the author of the change in the audit
    /// trail. Set by the controller, never taken from client input.
    /// </summary>
    public Guid UserId { get; init; }
}
