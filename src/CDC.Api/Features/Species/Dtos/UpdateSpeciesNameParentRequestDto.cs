namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Wire contract for <c>PUT /api/species/name-parent</c>. Deliberately excludes who made the
/// change - the API sets that from the authenticated caller rather than trusting the client.
/// </summary>
public sealed record UpdateSpeciesNameParentRequestDto
{
    /// <summary>Gets the species being updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the new display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the new parent identifier; <see cref="Guid.Empty"/> for a root species.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the row version last read for this species. The update is rejected with HTTP 409
    /// if it no longer matches the stored value.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
