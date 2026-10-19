using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the profile endpoints behind the Profile properties page: the defaults for a
/// cloned profile or scenario, creating a profile, and updating its titles and affected species.
/// </summary>
public interface IProfilePropertiesApiService
{
    /// <summary>Calls <c>GET /api/profiles/defaults</c> for the values a new profile or scenario starts with.</summary>
    /// <param name="cloneProfileVersionId">The profile version being cloned.</param>
    /// <param name="isWhatIfScenario">Whether the clone becomes a "what-if" scenario rather than a profile.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The defaults, or <see langword="null"/> when the version does not exist.</returns>
    Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profiles</c> to create a profile, its first draft version and its affected species.</summary>
    Task<CreateProfileResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/profiles/{profileId}</c> to update a profile's titles and affected species.</summary>
    Task<UpdateProfileAttributesResult> UpdateProfileAttributesAsync(
        Guid profileId,
        UpdateProfileAttributesRequest request,
        CancellationToken cancellationToken = default);
}
