namespace CDC.Common.Contracts;

/// <summary>
/// Which "Manage profile" page action links are visible for the current user and profile state.
/// Mirrors the legacy <c>ManageProfile.aspx.vb</c> <c>RefreshDisplay</c> method and the
/// authorisation rules in <c>ProfilesLibrary\Profiles\Profile.vb</c> and
/// <c>ProfilesLibrary\UserMaintenance\ProfileContributorList.vb</c> verbatim. Shared by CDC.Api's
/// response DTO and CDC.Web's view model so the two can never drift apart.
/// </summary>
public sealed record ManageProfileLinkVisibilityDto
{
    /// <summary>
    /// "Edit properties". Legacy: <c>Profile.CanEditProfile() AndAlso (profile.IsWhatIfScenario
    /// OrElse profile.CurrentDraftVersion IsNot Nothing)</c>.
    /// </summary>
    public bool CanEditProperties { get; init; }

    /// <summary>
    /// "Maintain contributors and reviewers". Legacy:
    /// <c>ProfileContributorList.CanGetContributorList()</c>.
    /// </summary>
    public bool CanMaintainContributorsAndReviewers { get; init; }

    /// <summary>
    /// "View contributions report". Legacy: <c>Profile.CanGetContributionsReport()</c> →
    /// <c>GetContributionsReportCommand.CanGetReport()</c>.
    /// </summary>
    public bool CanViewContributionsReport { get; init; }

    /// <summary>"Create new draft version". Legacy: <c>profile.CanCreateNewDraft()</c>.</summary>
    public bool CanCreateNewDraftVersion { get; init; }

    /// <summary>"Delete current version". Legacy: <c>profile.CanDeleteLatestDraft()</c>.</summary>
    public bool CanDeleteCurrentVersion { get; init; }

    /// <summary>
    /// "Clone new profile". Legacy: <c>Profile.CanCreateProfile() AndAlso Not
    /// profile.IsWhatIfScenario</c>.
    /// </summary>
    public bool CanCloneNewProfile { get; init; }

    /// <summary>"Clone new scenario". Legacy: <c>Profile.CanCreateProfile()</c>.</summary>
    public bool CanCloneNewScenario { get; init; }

    /// <summary>"Publish (public)". Legacy: <c>profile.CanPublishPublic()</c>.</summary>
    public bool CanPublishPublic { get; init; }

    /// <summary>"Publish (Defranet only)". Legacy: <c>profile.CanPublish()</c>.</summary>
    public bool CanPublishDefranetOnly { get; init; }

    /// <summary>
    /// "Allow public access" toggle. Legacy: visible unless <c>NOT
    /// SetProfileVersionPublicAccessCommand.CanChangePublicAccess(profile.Id) OrElse
    /// profile.CurrentPublishedVersion.Equals(profile.CurrentPublicVersion)</c>.
    /// </summary>
    public bool CanAllowPublicAccess { get; init; }
}
