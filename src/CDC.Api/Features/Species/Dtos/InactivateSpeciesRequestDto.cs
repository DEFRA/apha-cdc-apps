namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Wire contract for <c>PUT /api/species/{speciesId}/inactivate</c> on CDC.Api.
/// </summary>
public sealed record InactivateSpeciesRequestDto
{
    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the row version last read for this species.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
