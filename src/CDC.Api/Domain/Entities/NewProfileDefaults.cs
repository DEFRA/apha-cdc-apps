namespace CDC.Api.Domain.Entities;

/// <summary>
/// Default values for a new profile or "what-if" scenario, sourced from the profile version
/// being cloned. Mirrors the legacy <c>NewProfileDefaults</c> data contract returned by
/// <c>ProfileManagementService.GetNewProfileDefaults</c> (<c>spgProfileVersionInfoById</c>).
/// </summary>
public sealed record NewProfileDefaults
{
    /// <summary>Gets the default title for a new current-situation profile.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the default scenario title for a new "what-if" scenario.</summary>
    public required string ScenarioTitle { get; init; }

    /// <summary>
    /// Gets the identifier of the current-situation profile a new scenario would belong to.
    /// </summary>
    public required Guid ParentId { get; init; }

    /// <summary>Gets the parent (current-situation) profile's title.</summary>
    public required string ParentTitle { get; init; }

    /// <summary>Gets the cloned version's profile status.</summary>
    public required Guid ProfileStatusId { get; init; }

    /// <summary>Gets the species affected by the cloned profile version.</summary>
    public required IReadOnlyList<AffectedSpeciesInfo> AffectedSpecies { get; init; }
}
