using CDC.Common.Contracts;

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
public sealed record ProfileSearchResultDto : ProfileSearchResultContract<ProfileHistoryItemDto, ProfileScenarioDto>; // NOSONAR

/// <summary>
/// One "what-if" scenario belonging to a profile, and its own independent version history.
/// </summary>
public sealed record ProfileScenarioDto : ProfileScenarioContract<ProfileHistoryItemDto>; // NOSONAR

/// <summary>
/// A single history entry for a profile version.
/// </summary>
public sealed record ProfileHistoryItemDto : ProfileHistoryItemContract; // NOSONAR
