namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Outcome of <c>POST /api/species</c>.
/// </summary>
public sealed record AddSpeciesResultDto
{
    /// <summary>Gets the identifier assigned to the new species.</summary>
    public Guid SpeciesId { get; init; }
}
