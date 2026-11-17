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
    /// <param name="displayPublished">Include profiles with a published version.</param>
    /// <param name="displayDraft">Include profiles with a draft version.</param>
    /// <param name="displayScenarios">Return each profile's "what-if" scenarios too. It never makes a profile match on its own.</param>
    /// <param name="displayMyProfiles">Keep only the profiles the signed-in user is assigned to as an author or a reviewer: on the profile itself, or on
    /// one of its "what-if" scenarios while those are displayed. It has no effect for a user who is assigned to no profile, who is not
    /// offered the filter.</param>
    /// <param name="searchForType">Whether <paramref name="searchText"/> is matched as one phrase or as all of its words.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enriched profile search results.</returns>
    Task<IReadOnlyList<ProfileSearchResultDto>> GetProfileSearchResultsAsync(
        string? searchText = null,
        bool displayPublished = true,
        bool displayDraft = false,
        bool displayScenarios = false,
        bool displayMyProfiles = false,
        SearchForType searchForType = SearchForType.ExactWordOrPhrase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the signed-in user is offered the "Display my own profiles only" filter: only a user who is an
    /// author or a reviewer of at least one profile is (legacy <c>ProfilesIdentity.IsContributorOrReviewer</c>).
    /// </summary>
    bool CanFilterToMyProfiles { get; }

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
