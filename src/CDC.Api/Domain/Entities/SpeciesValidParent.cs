namespace CDC.Api.Domain.Entities;

/// <summary>
/// A species or species group that is a legal parent choice for another species: itself and
/// its own descendants are excluded, so the hierarchy cannot form a cycle. Sourced from
/// <c>spgSpeciesValidParents</c>.
/// </summary>
public sealed record SpeciesValidParent
{
    /// <summary>Gets the species identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }
}
