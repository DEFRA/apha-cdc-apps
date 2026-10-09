namespace CDC.Api.Features.Species.Dtos;

/// <summary>Wire contract for <c>PUT /api/species/{speciesId}/position</c>.</summary>
public sealed record ChangeSpeciesPositionRequestDto
{
    /// <summary>Gets a value indicating the move direction: true to swap with the previous sibling, false for the next.</summary>
    public bool IsMovingUp { get; init; }
}
