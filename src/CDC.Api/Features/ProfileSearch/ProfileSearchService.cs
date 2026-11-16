using CDC.Api.Application;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;

namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Default <see cref="IProfileSearchService"/>: reads through <see cref="IProfileRepository"/>
/// and applies the text/status/letter filtering the legacy UI expects. As in legacy, every draft version is listed to every
/// user (<c>ProfileInfoList.CanGetProfileInfoList</c>: "this data is available to all"); a draft is refused only when it is browsed.
/// </summary>
/// <param name="repository">Profile data access.</param>
/// <param name="userContext">The signed-in user, whose assignments to profiles decide the "Display my own profiles only" filter.</param>
public sealed class ProfileSearchService(IProfileRepository repository, IUserContext userContext) : IProfileSearchService
{
    /// <inheritdoc />
    public bool CanFilterToMyProfiles => userContext.AssignedProfileIds.Count > 0;

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
        bool displayMyProfiles = false,
        SearchForType searchForType = SearchForType.ExactWordOrPhrase,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = await repository.GetAllProfilesAsync(cancellationToken);
        var assignedProfileIds = userContext.AssignedProfileIds;
        var onlyMyProfiles = displayMyProfiles && assignedProfileIds.Count > 0;

        return
        [
            .. allProfiles
                .Where(profile => MatchesVersionFilter(profile, displayPublished, displayDraft)
                    && TitleMatches(profile.Title, searchText, searchForType)
                    && (!onlyMyProfiles || IsAssignedTo(profile, assignedProfileIds, displayScenarios)))
                // As in legacy, the scenarios are listed only when asked for.
                .Select(profile => displayScenarios ? profile : profile with { WhatIfScenarios = [] })
        ];
    }

    /// <summary>Mirrors the legacy <c>ProfileInfoList.Filter</c>'s "my profiles" test: the user is assigned to the profile's own lineage as
    /// an author or a reviewer, or to one of its "what-if" scenarios while scenarios are displayed.</summary>
    private static bool IsAssignedTo(ProfileSearchResultDto profile, IReadOnlySet<Guid> assignedProfileIds, bool displayScenarios) =>
        assignedProfileIds.Contains(profile.Id)
        || (displayScenarios && profile.WhatIfScenarios.Any(scenario => assignedProfileIds.Contains(scenario.ScenarioId)));

    /// <summary>Mirrors the legacy <c>ProfileInfoList.Filter</c>: a profile matches when any of its
    /// scenarios - its own, or a "what-if" - has a published version and published versions are
    /// displayed, or has a draft version and draft versions are displayed. A profile can have both
    /// at once, so a published and draft profile still matches when only "display draft" is on.
    /// Displaying scenarios does not make a profile match on its own.</summary>
    internal static bool MatchesVersionFilter(ProfileSearchResultDto profile, bool displayPublished, bool displayDraft)
    {
        var hasPublishedVersion = profile.PublishedVersions.Count > 0
            || profile.WhatIfScenarios.Any(scenario => scenario.PublishedVersions.Count > 0);
        var hasDraftVersion = profile.DraftVersions.Count > 0
            || profile.WhatIfScenarios.Any(scenario => scenario.DraftVersions.Count > 0);

        return (displayPublished && hasPublishedVersion) || (displayDraft && hasDraftVersion);
    }

    /// <summary>Mirrors the legacy <c>ProfileInfoList.TitleMatchesWords</c>: an exact-phrase search
    /// matches the trimmed search text as a single substring, an all-words search splits it on
    /// spaces and requires every word to appear somewhere in the title, in any order.</summary>
    internal static bool TitleMatches(string title, string? searchText, SearchForType searchForType)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        // Legacy splits on the space character only, discarding empty entries.
        var words = searchForType == SearchForType.AllWords
            ? searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [searchText.Trim()];

        return words.All(word => title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSearchResultDto>> GetProfilesByLetterAsync(
        string letter,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = await repository.GetAllProfilesAsync(cancellationToken);

        return
        [
            .. allProfiles
                .Where(profile => letter == "All" || profile.Title.StartsWith(letter, StringComparison.OrdinalIgnoreCase))
        ];
    }
}
