namespace CDC.Web.Models;

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

/// <summary>Result of a call to <c>DELETE /api/species/{speciesId}</c>.</summary>
public sealed record DeleteSpeciesResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
