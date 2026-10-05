namespace CDC.Common.Contracts;

/// <summary>
/// Comprehensive profile information for search results, including version history. Shared by
/// CDC.Api's response DTO and CDC.Web's view model so the wire shape can never drift between the
/// two. Generic over the concrete history-item/scenario types so each project's nested
/// collections are typed as its own concrete records rather than this base contract.
/// </summary>
public abstract record ProfileSearchResultContract<THistoryItem, TScenario>
    where THistoryItem : ProfileHistoryItemContract
    where TScenario : ProfileScenarioContract<THistoryItem>
{
    /// <summary>Gets the profile identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the profile name/title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the profile status (Draft, Published, Scenario).</summary>
    public required string Status { get; init; }

    /// <summary>Gets the date the profile was created.</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>Gets the date the profile was last modified.</summary>
    public required DateTime ModifiedAtUtc { get; init; }

    /// <summary>Gets a value indicating whether the profile is publicly visible.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets the affected species for this profile.</summary>
    public required IReadOnlyList<string> AffectedSpecies { get; init; }

    /// <summary>Gets the published versions of this profile.</summary>
    public required IReadOnlyList<THistoryItem> PublishedVersions { get; init; }

    /// <summary>Gets the draft versions of this profile.</summary>
    public required IReadOnlyList<THistoryItem> DraftVersions { get; init; }

    /// <summary>
    /// Gets the "what-if" scenarios belonging to this profile. Each scenario is its own version
    /// lineage, with its own independent published/draft version history - never merged with the
    /// profile's own (current-situation) history above, or with any other scenario's.
    /// </summary>
    public required IReadOnlyList<TScenario> WhatIfScenarios { get; init; }
}

/// <summary>
/// One "what-if" scenario belonging to a profile, and its own independent version history.
/// Generic over the concrete history-item type so each project's nested collections are typed
/// as its own concrete record rather than this base contract.
/// </summary>
public abstract record ProfileScenarioContract<THistoryItem>
    where THistoryItem : ProfileHistoryItemContract
{
    /// <summary>Gets the scenario's identifier (its own root, distinct from the parent profile's).</summary>
    public required Guid ScenarioId { get; init; }

    /// <summary>Gets this scenario's own published versions.</summary>
    public required IReadOnlyList<THistoryItem> PublishedVersions { get; init; }

    /// <summary>Gets this scenario's own draft versions.</summary>
    public required IReadOnlyList<THistoryItem> DraftVersions { get; init; }
}

/// <summary>A single history entry for a profile version.</summary>
public abstract record ProfileHistoryItemContract
{
    /// <summary>Gets the version identifier.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Gets the major component of the version number, displayed as <c>Major.Minor</c>.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>Gets the minor component of the version number.</summary>
    public int VersionMinor { get; init; }

    /// <summary>Gets the version title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the date this version became effective.</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>Gets the date this version stopped being effective, or <see langword="null"/> while
    /// it is still in effect.</summary>
    public DateTime? EffectiveToUtc { get; init; }

    /// <summary>Gets a value indicating whether this is a scenario.</summary>
    public required bool IsScenario { get; init; }
}

