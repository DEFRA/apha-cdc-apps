using CDC.Api.Features.ProfileSearch.Dtos;

namespace CDC.Api.Features.ProfileSearch.Interfaces;

/// <summary>
/// Reads profile search data.
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
}
