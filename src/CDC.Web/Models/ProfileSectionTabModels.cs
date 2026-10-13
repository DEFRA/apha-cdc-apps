namespace CDC.Web.Models;

/// <summary>The tabs of a profile section, in the order the legacy <c>tabStrip</c> lists them. Each tab is its own Razor Page.</summary>
public enum ProfileSectionTab
{
    /// <summary>The section's questions (legacy <c>EditProfileQuestions.aspx</c>); its tab text is the section's short name.</summary>
    Questions,

    /// <summary>Scientific paper and legislative references (<c>EditProfileReferences.aspx</c>).</summary>
    References,

    /// <summary>Sources of further information (<c>EditProfileFurtherInformation.aspx</c>).</summary>
    FurtherInformation,

    /// <summary>Review comments (<c>EditProfileReviewComments.aspx</c>); draft versions only.</summary>
    ReviewComments,

    /// <summary>Every section's revision dates (<c>EditProfileAllRevisionDates.aspx</c>); draft versions only.</summary>
    AllRevisionDates,

    /// <summary>Logged contributions (<c>EditProfileContributions.aspx</c>); draft versions only.</summary>
    Contributions
}

/// <summary>One entry in the tab strip shown above a profile section's content.</summary>
/// <param name="Tab">The tab this entry navigates to.</param>
/// <param name="Text">The text shown on the tab.</param>
/// <param name="Url">The URL of the tab's page for the current profile and section.</param>
/// <param name="IsActive">Whether this is the tab of the page being shown.</param>
public sealed record ProfileSectionTabItem(ProfileSectionTab Tab, string Text, string? Url, bool IsActive);

/// <summary>Everything the tab strip partial renders.</summary>
/// <param name="Tabs">The visible tabs, in order.</param>
/// <param name="SubmitUrl">When set, each tab is a submit button posting to this URL with the tab's name, so the
/// page can save its edits before navigating (legacy Questions page); otherwise each tab is a plain link.</param>
public sealed record ProfileSectionTabsViewModel(IReadOnlyList<ProfileSectionTabItem> Tabs, string? SubmitUrl = null);
