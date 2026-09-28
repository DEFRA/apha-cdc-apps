namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Result of a successful name/parent update: the new row version, for further edits.
/// </summary>
public sealed record UpdateSpeciesNameParentResultDto
{
    /// <summary>Gets the species that was updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the new row version.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
