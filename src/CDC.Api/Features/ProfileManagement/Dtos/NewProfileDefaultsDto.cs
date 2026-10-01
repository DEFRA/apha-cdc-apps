namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// Default values for a new profile or "what-if" scenario, sourced from the profile version
/// being cloned. Returned by <c>GET /api/profiles/defaults</c>.
/// </summary>
public sealed record NewProfileDefaultsDto
{
    /// <summary>Gets the default title for a new current-situation profile.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the default scenario title for a new "what-if" scenario.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the current-situation profile a new scenario would belong to.
    /// </summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the parent (current-situation) profile's title.</summary>
    public string ParentTitle { get; init; } = string.Empty;

    /// <summary>Gets the cloned version's profile status.</summary>
    public Guid ProfileStatusId { get; init; }

    /// <summary>Gets the species affected by the cloned profile version.</summary>
    public IReadOnlyList<AffectedSpeciesDto> AffectedSpecies { get; init; } = [];
}
