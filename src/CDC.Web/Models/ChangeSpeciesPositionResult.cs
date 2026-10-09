namespace CDC.Web.Models;

/// <summary>Result of a call to <c>PUT /api/species/{speciesId}/position</c>.</summary>
public sealed record ChangeSpeciesPositionResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
