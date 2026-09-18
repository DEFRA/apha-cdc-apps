using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A species or species group. The hierarchy is expressed through <see cref="ParentId"/>;
/// the legacy WCF service flattened the same tree before returning it.
/// </summary>
public sealed record Species : BaseEntity
{
    /// <summary>Gets the identifier of the parent species group.</summary>
    public required Guid ParentId { get; init; }

    /// <summary>Gets the display name. Sourced from the <c>Species.Name</c> column.</summary>
    public required string Description { get; init; }

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public required bool IsInUse { get; init; }
}
