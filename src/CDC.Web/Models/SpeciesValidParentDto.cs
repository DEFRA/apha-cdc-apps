namespace CDC.Web.Models;

/// <summary>
/// A species or species group that is a legal parent choice for another species, as returned
/// by <c>GET /api/species/{id}/valid-parents</c> on CDC.Api.
/// </summary>
public sealed record SpeciesValidParentDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;
}
