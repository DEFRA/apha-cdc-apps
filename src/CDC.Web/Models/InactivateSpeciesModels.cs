namespace CDC.Web.Models;

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

/// <summary>Result of a call to <c>PUT /api/species/{speciesId}/inactivate</c>.</summary>
public sealed record InactivateSpeciesResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
