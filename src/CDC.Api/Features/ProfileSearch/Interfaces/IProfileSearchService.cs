using CDC.Api.Features.ProfileSearch.Dtos;

namespace CDC.Api.Features.ProfileSearch.Interfaces;

/// <summary>
/// Reads profile search data and provides filtering capabilities for the application.
/// </summary>
public interface IProfileSearchService
{
    /// <summary>
    /// Gets all profile summaries.
    /// </summary>
    Task<IReadOnlyList<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a profile version by identifier.
    /// </summary>
    Task<ProfileVersionDto?> GetProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets enriched search results for profiles with comprehensive metadata and filtering.
    /// </summary>
    /// <param name="searchText">Optional text to search in profile titles.</param>
    /// <param name="displayPublished">Include published versions.</param>
    /// <param name="displayDraft">Include draft versions.</param>
    /// <param name="displayScenarios">Include scenario versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enriched profile search results.</returns>
    Task<IReadOnlyList<ProfileSearchResultDto>> GetProfileSearchResultsAsync(
        string? searchText = null,
        bool displayPublished = true,
        bool displayDraft = false,
        bool displayScenarios = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets profiles starting with a specific letter.
    /// </summary>
    /// <param name="letter">The letter to filter by (or "All" for all profiles).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Profiles matching the letter filter.</returns>
    Task<IReadOnlyList<ProfileSearchResultDto>> GetProfilesByLetterAsync(
        string letter,
        CancellationToken cancellationToken = default);
}
