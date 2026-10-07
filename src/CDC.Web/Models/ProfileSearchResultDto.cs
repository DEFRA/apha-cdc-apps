using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>
/// Comprehensive profile information for search results, including version history, as returned
/// by <c>GET /api/profile-search/search</c>.
/// </summary>
public sealed record ProfileSearchResultDto : ProfileSearchResultContract<ProfileHistoryItemDto, ProfileScenarioDto>; // NOSONAR

/// <summary>One "what-if" scenario belonging to a profile, and its own independent version history.</summary>
public sealed record ProfileScenarioDto : ProfileScenarioContract<ProfileHistoryItemDto>; // NOSONAR

/// <summary>A single history entry for a profile version.</summary>
public sealed record ProfileHistoryItemDto : ProfileHistoryItemContract; // NOSONAR

