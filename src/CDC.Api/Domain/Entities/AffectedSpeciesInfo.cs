namespace CDC.Api.Domain.Entities;

/// <summary>
/// A species affected by a profile or profile version. Mirrors the legacy
/// <c>AffectedSpecies</c> data contract used by <c>ProfileManagementService</c>.
/// </summary>
public sealed record AffectedSpeciesInfo
{
    /// <summary>Gets the species identifier.</summary>
    public required Guid SpeciesId { get; init; }

    /// <summary>Gets the species display name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the affected species type, either <c>Profiled</c> or <c>Other</c>, as stored by
    /// <c>AffectedSpeciesTypeName</c> in the database.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public required bool IsActive { get; init; }
}
