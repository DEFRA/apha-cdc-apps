namespace CDC.Api.Features.ProfileSearch.Dtos;

/// <summary>
/// Summary information for a profile.
/// </summary>
public sealed record ProfileDto
{
    /// <summary>Gets or sets the profile identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets or sets the profile name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the profile status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets or sets a value indicating whether the profile is active.</summary>
    public required bool IsActive { get; init; }
}

/// <summary>
/// Comprehensive profile information for search results including version history.
/// </summary>
public sealed record ProfileSearchResultDto
{
    /// <summary>Gets or sets the profile identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets or sets the profile name/title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the profile status (Draft, Published, Scenario).</summary>
    public required string Status { get; init; }

    /// <summary>Gets or sets the date the profile was created.</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>Gets or sets the date the profile was last modified.</summary>
    public required DateTime ModifiedAtUtc { get; init; }

    /// <summary>Gets or sets a value indicating whether the profile is publicly visible.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets or sets the affected species for this profile.</summary>
    public required IReadOnlyList<string> AffectedSpecies { get; init; }

    /// <summary>Gets or sets the published versions of this profile.</summary>
    public required IReadOnlyList<ProfileHistoryItemDto> PublishedVersions { get; init; }

    /// <summary>Gets or sets the draft versions of this profile.</summary>
    public required IReadOnlyList<ProfileHistoryItemDto> DraftVersions { get; init; }

    /// <summary>
    /// Gets or sets the "what-if" scenarios belonging to this profile. Each scenario is its own
    /// version lineage, with its own independent published/draft version history - never merged
    /// with the profile's own (current-situation) history above, or with any other scenario's.
    /// </summary>
    public required IReadOnlyList<ProfileScenarioDto> WhatIfScenarios { get; init; }
}

/// <summary>
/// One "what-if" scenario belonging to a profile, and its own independent version history.
/// </summary>
public sealed record ProfileScenarioDto
{
    /// <summary>Gets or sets the scenario's identifier (its own root, distinct from the parent profile's).</summary>
    public required Guid ScenarioId { get; init; }

    /// <summary>Gets or sets this scenario's own published versions.</summary>
    public required IReadOnlyList<ProfileHistoryItemDto> PublishedVersions { get; init; }

    /// <summary>Gets or sets this scenario's own draft versions.</summary>
    public required IReadOnlyList<ProfileHistoryItemDto> DraftVersions { get; init; }
}

/// <summary>
/// A single history entry for a profile version.
/// </summary>
public sealed record ProfileHistoryItemDto
{
    /// <summary>Gets or sets the version identifier.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Gets or sets the version number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>Gets or sets the version title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the date this version was created.</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>Gets or sets a value indicating whether this is a scenario.</summary>
    public required bool IsScenario { get; init; }
}
