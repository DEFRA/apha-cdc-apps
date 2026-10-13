using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Tab routing shared by every profile section page. Mirrors the legacy <c>tabStrip</c>: the same six tabs in
/// the same order, each tab opening its own page, with Review comments, All Revision dates and Contributions
/// shown only when the user may see them.
/// </summary>
public static class ProfileSectionTabs
{
    /// <summary>Gets the Razor Page path that implements a tab.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The page path, for example <c>/SurveillanceProfiles/EditProfileReferences</c>.</returns>
    public static string PagePath(ProfileSectionTab tab) => tab switch
    {
        ProfileSectionTab.Questions => "/SurveillanceProfiles/EditProfileQuestions",
        ProfileSectionTab.References => "/SurveillanceProfiles/EditProfileReferences",
        ProfileSectionTab.FurtherInformation => "/SurveillanceProfiles/EditProfileFurtherInformation",
        ProfileSectionTab.ReviewComments => "/SurveillanceProfiles/EditProfileReviewComments",
        ProfileSectionTab.AllRevisionDates => "/SurveillanceProfiles/EditProfileAllRevisionDates",
        ProfileSectionTab.Contributions => "/SurveillanceProfiles/EditProfileContributions",
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "Unknown profile section tab.")
    };

    /// <summary>Gets the tab text for every tab except <see cref="ProfileSectionTab.Questions"/>, whose text is the section name.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The legacy tab text.</returns>
    public static string Text(ProfileSectionTab tab) => tab switch
    {
        ProfileSectionTab.References => "References",
        ProfileSectionTab.FurtherInformation => "Further information",
        ProfileSectionTab.ReviewComments => "Review comments",
        ProfileSectionTab.AllRevisionDates => "All Revision dates",
        ProfileSectionTab.Contributions => "Contributions",
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "The Questions tab is named after the section.")
    };

    /// <summary>Lists the tabs the user can see.</summary>
    /// <param name="canViewReviewComments">Whether the Review comments tab is allowed.</param>
    /// <param name="canViewAllRevisionDates">Whether the All Revision dates tab is allowed.</param>
    /// <param name="canViewContributions">Whether the Contributions tab is allowed.</param>
    /// <returns>The visible tabs, in legacy order.</returns>
    public static IReadOnlyList<ProfileSectionTab> VisibleTabs(bool canViewReviewComments, bool canViewAllRevisionDates, bool canViewContributions)
    {
        var tabs = new List<ProfileSectionTab> { ProfileSectionTab.Questions, ProfileSectionTab.References, ProfileSectionTab.FurtherInformation };

        if (canViewReviewComments)
        {
            tabs.Add(ProfileSectionTab.ReviewComments);
        }

        if (canViewAllRevisionDates)
        {
            tabs.Add(ProfileSectionTab.AllRevisionDates);
        }

        if (canViewContributions)
        {
            tabs.Add(ProfileSectionTab.Contributions);
        }

        return tabs;
    }

    /// <summary>Formats a section's short name for its tab, matching legacy <c>FormattedShortName</c>.</summary>
    /// <param name="shortName">The section's short name.</param>
    /// <returns>The short name with only its first letter capitalised and " and " shown as " &amp; ".</returns>
    public static string FormatShortName(string shortName) =>
        string.IsNullOrEmpty(shortName)
            ? string.Empty
            : string.Concat(shortName[..1], shortName[1..].ToLowerInvariant().Replace(" and ", " & ", StringComparison.Ordinal));
}
