namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// A species affected by a profile or profile version.
/// </summary>
public sealed record AffectedSpeciesDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the affected species type: <c>Profiled</c> or <c>Other</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public bool IsActive { get; init; }
}
