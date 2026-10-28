namespace CDC.Web.Models;

/// <summary>
/// The subset of a profile's attributes needed by CDC.Web, as returned by
/// <c>GET /api/profiles/{profileId}/attributes</c>.
/// </summary>
public sealed record ProfileAttributesDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>The "what-if" scenario title, empty for a current-situation profile.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>The current-situation profile this scenario belongs to, or <see cref="Guid.Empty"/> when this profile is not a scenario.</summary>
    public Guid ParentId { get; init; }

    /// <summary>The parent (current-situation) profile's title.</summary>
    public string ParentTitle { get; init; } = string.Empty;

    /// <summary>The current draft profile version, or <see cref="Guid.Empty"/> when the profile has no draft.</summary>
    public Guid CurrentDraftProfileVersionId { get; init; }

    /// <summary>The current published profile version, or <see cref="Guid.Empty"/> when the profile has never been published.</summary>
    public Guid CurrentPublishedProfileVersionId { get; init; }

    /// <summary>The SQL Server <c>rowversion</c> to send back when updating.</summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>The species affected by the current draft version.</summary>
    public IReadOnlyList<AffectedSpeciesDto> AffectedSpecies { get; init; } = [];

    /// <summary>Whether this profile is a "what-if" scenario rather than a current-situation profile.</summary>
    public bool IsWhatIfScenario => ParentId != Guid.Empty;
}
