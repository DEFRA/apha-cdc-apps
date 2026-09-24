namespace CDC.Api.Domain.Entities;

/// <summary>
/// The name/parent detail of a single species, as read for the "Edit name/parent" screen.
/// Sourced from <c>spgSpeciesById</c>.
/// </summary>
public sealed record SpeciesDetail
{
    /// <summary>Gets the species identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the current display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the identifier of the current parent species group.</summary>
    public required Guid ParentId { get; init; }

    /// <summary>Gets the current parent's display name; empty when the species is a root.</summary>
    public required string ParentName { get; init; }

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public required bool IsInUse { get; init; }

    /// <summary>Gets the number of direct child species or groups.</summary>
    public required int ChildCount { get; init; }

    /// <summary>Gets the number of direct child species or groups that are active.</summary>
    public required int ActiveChildCount { get; init; }

    /// <summary>Gets the row version, used for optimistic concurrency on update.</summary>
    public required byte[] LastUpdated { get; init; }
}
