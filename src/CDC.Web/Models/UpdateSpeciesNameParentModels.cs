namespace CDC.Web.Models;

/// <summary>
/// Wire contract for <c>PUT /api/species/name-parent</c> on CDC.Api.
/// </summary>
public sealed record UpdateSpeciesNameParentRequestDto
{
    /// <summary>Gets the species being updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the new display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the new parent identifier; <see cref="Guid.Empty"/> for a root species.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the row version last read for this species.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>
/// Outcome of a call to <c>PUT /api/species/name-parent</c>, distinguishing the failure kinds
/// the page needs to react to differently.
/// </summary>
public enum SpeciesUpdateOutcome
{
    /// <summary>The change was saved.</summary>
    Success,

    /// <summary>The request failed validation (for example, a missing reason for change).</summary>
    ValidationFailed,

    /// <summary>Another user saved a change to this species since it was read.</summary>
    Conflict,

    /// <summary>The call to CDC.Api failed unexpectedly.</summary>
    Error
}

/// <summary>Result of a call to <c>PUT /api/species/name-parent</c>.</summary>
public sealed record UpdateSpeciesNameParentResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
