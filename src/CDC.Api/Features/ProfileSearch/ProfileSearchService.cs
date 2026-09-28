using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;

namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Default <see cref="IProfileSearchService"/>: reads through <see cref="IProfileRepository"/>
/// and applies the text/status/letter filtering the legacy UI expects.
/// </summary>
/// <param name="repository">Profile data access.</param>
public sealed class ProfileSearchService(IProfileRepository repository) : IProfileSearchService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = await repository.GetAllProfilesAsync(cancellationToken);

        return [.. profiles.Select(profile => new ProfileDto
        {
            Id = profile.Id,
            Name = profile.Title,
            Status = profile.Status,
            IsActive = true
        })];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Not yet backed by the database: <c>spgaProfile</c> does not return version content, so a
    /// dedicated stored procedure is required before this can read real data. Returns a
    /// placeholder version until that procedure exists.
    /// </remarks>
    public Task<ProfileVersionDto?> GetProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var version = new ProfileVersionDto
        {
            Id = profileVersionId,
            ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VersionNumber = 1,
            Title = "Profile Version 1",
            Content = "Comprehensive disease profile details including epidemiology, risk factors, and mitigation strategies.",
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        return Task.FromResult<ProfileVersionDto?>(version);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSearchResultDto>> GetProfileSearchResultsAsync(
        string? searchText = null,
        bool displayPublished = true,
        bool displayDraft = false,
        bool displayScenarios = false,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = await repository.GetAllProfilesAsync(cancellationToken);

        return
        [
            .. allProfiles.Where(profile =>
            {
                var matchesStatus = (displayPublished && profile.Status == "Published") ||
                                     (displayDraft && profile.Status == "Draft") ||
                                     (displayScenarios && profile.Status == "Scenario");

                if (!matchesStatus)
                {
                    return false;
                }

                return string.IsNullOrEmpty(searchText) ||
                       profile.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            })
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSearchResultDto>> GetProfilesByLetterAsync(
        string letter,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = await repository.GetAllProfilesAsync(cancellationToken);

        return
        [
            .. allProfiles.Where(profile =>
                letter == "All" || profile.Title.StartsWith(letter, StringComparison.OrdinalIgnoreCase))
        ];
    }
}
