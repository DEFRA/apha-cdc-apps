namespace CDC.Web.Models;

/// <summary>
/// Wire contract for <c>POST /api/species</c> on CDC.Api.
/// </summary>
public sealed record AddSpeciesRequestDto
{
    /// <summary>Gets the display name of the new species.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the chosen parent. <see langword="null"/> means no choice was made;
    /// <see cref="Guid.Empty"/> means the new species sits at the root of the hierarchy.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Response body of a successful <c>POST /api/species</c>.</summary>
public sealed record AddSpeciesResultDto
{
    /// <summary>Gets the identifier assigned to the new species.</summary>
    public Guid SpeciesId { get; init; }
}

/// <summary>Result of a call to <c>POST /api/species</c>.</summary>
public sealed record AddSpeciesResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets the identifier assigned to the new species, when the call succeeded.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
