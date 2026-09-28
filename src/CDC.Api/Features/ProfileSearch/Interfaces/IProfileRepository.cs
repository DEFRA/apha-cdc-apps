using CDC.Api.Features.ProfileSearch.Dtos;

namespace CDC.Api.Features.ProfileSearch.Interfaces;

/// <summary>
/// Data access for profiles and their version history. Maps onto the legacy
/// <c>Profiles.DataAccess.Sql.ProfileSearchService</c> stored procedure call, so behaviour is
/// preserved. Implementations must contain no business logic.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Reads every profile, with its published/draft versions and scenarios, via <c>spgaProfile</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every profile, in the stored procedure's title order.</returns>
    Task<IReadOnlyList<ProfileSearchResultDto>> GetAllProfilesAsync(CancellationToken cancellationToken);
}
