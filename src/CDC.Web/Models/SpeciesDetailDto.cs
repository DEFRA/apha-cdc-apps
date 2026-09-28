namespace CDC.Web.Models;

/// <summary>
/// The name/parent detail of a single species, as returned by <c>GET /api/species/{id}/detail</c>
/// on CDC.Api. Field names and types mirror the API's <c>SpeciesDetailDto</c> exactly.
/// </summary>
public sealed record SpeciesDetailDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the current display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the current parent species group.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the current parent's display name; empty when the species is a root.</summary>
    public string ParentName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public bool IsInUse { get; init; }

    /// <summary>Gets the number of direct child species or groups.</summary>
    public int ChildCount { get; init; }

    /// <summary>Gets the number of direct child species or groups that are active.</summary>
    public int ActiveChildCount { get; init; }

    /// <summary>Gets the row version. Send back unchanged when updating, for concurrency detection.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
