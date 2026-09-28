namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// A profile's attributes, current version pointers and affected species. Returned by
/// <c>GET /api/profiles/{profileId}/attributes</c>.
/// </summary>
public sealed record ProfileAttributesDto
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the profile title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the "what-if" scenario title, empty for a current-situation profile.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the current-situation profile this scenario belongs to, or
    /// <see cref="Guid.Empty"/> when this profile is not a scenario.
    /// </summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the parent (current-situation) profile's title.</summary>
    public string ParentTitle { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the current draft profile version.</summary>
    public Guid CurrentDraftProfileVersionId { get; init; }

    /// <summary>Gets the identifier of the current published profile version.</summary>
    public Guid CurrentPublishedProfileVersionId { get; init; }

    /// <summary>Gets the identifier of the current publicly visible profile version.</summary>
    public Guid CurrentPublicVersionId { get; init; }

    /// <summary>Gets a value indicating whether any scenario of this profile is public.</summary>
    public bool HasPublicScenarios { get; init; }

    /// <summary>Gets the profile's current status.</summary>
    public Guid ProfileStatusId { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> to send back when updating.</summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the species affected by this profile's current version.</summary>
    public IReadOnlyList<AffectedSpeciesDto> AffectedSpecies { get; init; } = [];
}
