namespace CDC.Web.Models;

/// <summary>
/// Comprehensive profile information for search results, including version history, as returned
/// by <c>GET /api/profile-search/search</c>.
/// </summary>
public sealed record ProfileSearchResultDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime ModifiedAtUtc { get; init; }
    public required bool IsPublic { get; init; }
    public required IReadOnlyList<string> AffectedSpecies { get; init; }
    public required IReadOnlyList<ProfileHistoryItemDto> PublishedVersions { get; init; }
    public required IReadOnlyList<ProfileHistoryItemDto> DraftVersions { get; init; }

    /// <summary>The profile's "what-if" scenarios. Each is its own version lineage, with its own
    /// independent published/draft history - never merged with the profile's own (current-situation)
    /// history above, or with any other scenario's.</summary>
    public required IReadOnlyList<ProfileScenarioDto> WhatIfScenarios { get; init; }
}

/// <summary>One "what-if" scenario belonging to a profile, and its own independent version history.</summary>
public sealed record ProfileScenarioDto
{
    public required Guid ScenarioId { get; init; }
    public required IReadOnlyList<ProfileHistoryItemDto> PublishedVersions { get; init; }
    public required IReadOnlyList<ProfileHistoryItemDto> DraftVersions { get; init; }
}

/// <summary>A single history entry for a profile version.</summary>
public sealed record ProfileHistoryItemDto
{
    public required Guid VersionId { get; init; }

    /// <summary>The major component of the version number, displayed as <c>Major.Minor</c>.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The minor component of the version number.</summary>
    public int VersionMinor { get; init; }

    public required string Title { get; init; }

    /// <summary>The date this version became effective.</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>The date this version stopped being effective, or <see langword="null"/> while it
    /// is still in effect.</summary>
    public DateTime? EffectiveToUtc { get; init; }

    public required bool IsScenario { get; init; }
}
