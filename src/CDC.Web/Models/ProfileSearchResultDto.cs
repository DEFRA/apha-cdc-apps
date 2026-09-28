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
    public required IReadOnlyList<ProfileHistoryItemDto> Scenarios { get; init; }
}

/// <summary>A single history entry for a profile version.</summary>
public sealed record ProfileHistoryItemDto
{
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required string Title { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required bool IsScenario { get; init; }
}
