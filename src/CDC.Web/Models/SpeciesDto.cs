namespace CDC.Web.Models;

/// <summary>
/// A species or species group returned by <c>GET /api/species</c> on CDC.Api. Field names and
/// types mirror the API's <c>SpeciesDto</c> exactly, so this deserialises directly from JSON.
/// </summary>
public sealed record SpeciesDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the parent species group, or <see cref="Guid.Empty"/> for a root node.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public bool IsInUse { get; init; }
}
