namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Outcome of a successful species answer data update.
/// </summary>
public sealed record UpdateSpeciesAnswerDataResultDto
{
    /// <summary>Gets the species that was updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>
    /// Gets the new row version of the species record, base64 encoded. Clients must use this
    /// value on their next update.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
