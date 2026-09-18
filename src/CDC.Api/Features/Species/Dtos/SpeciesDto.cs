namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// A species or species group.
/// </summary>
public sealed record SpeciesDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the parent species group.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public bool IsInUse { get; init; }
}
