namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Wire contract for <c>DELETE /api/species/{speciesId}</c> on CDC.Api.
/// </summary>
public sealed record DeleteSpeciesRequestDto
{
    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the row version last read for this species.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
