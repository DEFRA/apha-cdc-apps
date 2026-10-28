namespace CDC.Common.Contracts;

/// <summary>
/// The details shown on the "Manage profile" page: titles, version pointers and status. Returned
/// by <c>GET /api/profiles/{profileId}/manage</c>. Shared by CDC.Api's response DTO and CDC.Web's
/// view model so the wire shape can never drift between the two.
/// </summary>
public abstract record ManageProfileContract
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile title.</summary>
    public string ProfileTitle { get; init; } = string.Empty;

    /// <summary>Gets the "what-if" scenario title, empty for a current-situation profile.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this profile is a "what-if" scenario, as opposed
    /// to a current-situation profile.</summary>
    public bool IsWhatIfScenario { get; init; }

    /// <summary>
    /// Gets the profile's full display title, matching the legacy
    /// <c>ProfileVersionInfo.FullTitle</c>: the scenario title in brackets after the profile
    /// title, but only for a "what-if" scenario - never for a current-situation profile.
    /// </summary>
    public string FullTitle => IsWhatIfScenario ? $"{ProfileTitle} ({ScenarioTitle})" : ProfileTitle;

    /// <summary>Gets a display label for the latest published version visible to the public.</summary>
    public string LatestPublishedVersionPublic { get; init; } = string.Empty;

    /// <summary>Gets a display label for the latest published version visible on DefraNet only.</summary>
    public string LatestPublishedVersionDefraNetOnly { get; init; } = string.Empty;

    /// <summary>Gets a display label for the latest draft version.</summary>
    public string LatestDraftVersion { get; init; } = string.Empty;

    /// <summary>Gets a display label for the profile's current status.</summary>
    public string ProfileStatus { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the profile's current status, for pre-selecting the
    /// status dropdown. <see cref="Guid.Empty"/> when no status is set.</summary>
    public Guid ProfileStatusId { get; init; }

    /// <summary>
    /// Gets the profile version to browse/edit: the current draft when one exists, otherwise the
    /// current published version, otherwise the current public version. <see cref="Guid.Empty"/>
    /// when the profile has no version at all.
    /// </summary>
    public Guid CurrentProfileVersionId { get; init; }

    /// <summary>
    /// Gets a display label for the version a new draft would be created as - the legacy
    /// <c>LatestVersion</c> (current draft, else current published; never the public version)
    /// with its minor number incremented by one, for example "11.5". Empty when the profile has
    /// neither a draft nor a published version to base a new one on.
    /// </summary>
    public string NewDraftVersionLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the legacy <c>LatestVersion.Id</c>: the current draft, else the current published
    /// version - never the public version. This is the source version a new draft/published
    /// version is created from. <see cref="Guid.Empty"/> when the profile has neither.
    /// </summary>
    public Guid LatestVersionId { get; init; }

    /// <summary>Gets which action links are visible for the current user and profile state.</summary>
    public ManageProfileLinkVisibilityDto LinkVisibility { get; init; } = new();

    /// <summary>Gets the major.minor version number of <see cref="CurrentProfileVersionId"/>, for
    /// example "13.0". Empty when the profile has no current version.</summary>
    public string CurrentVersionNumber { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether <see cref="CurrentProfileVersionId"/>'s status is
    /// "Published" rather than "Draft".</summary>
    public bool CurrentVersionIsPublished { get; init; }

    /// <summary>Gets the date <see cref="CurrentProfileVersionId"/> became effective.</summary>
    public DateTime? CurrentVersionValidFrom { get; init; }

    /// <summary>Gets the date <see cref="CurrentProfileVersionId"/> was (or will be) superseded.
    /// Only meaningful for a draft version; <see langword="null"/> for a current published
    /// version.</summary>
    public DateTime? CurrentVersionValidTo { get; init; }

    /// <summary>
    /// Gets the profile title and current version summary shown beneath it on
    /// <c>EditProfileQuestions.aspx</c>, matching <c>ProfileVersionInfo.VersionToString()</c>
    /// exactly: <c>"Version {number} (Published) Valid from {date}"</c> when there is no
    /// effective-to date; <c>"... Valid from {date} to {date}"</c> when there is one; and, only
    /// for a current draft with a non-empty <see cref="ProfileStatus"/>, that status name
    /// appended in brackets (for example "(Validation complete)"). <see langword="null"/> when
    /// the profile has no current version.
    /// </summary>
    public string? CurrentVersionDisplay
    {
        get
        {
            if (CurrentProfileVersionId == Guid.Empty || string.IsNullOrEmpty(CurrentVersionNumber))
            {
                return null;
            }

            var statusWord = CurrentVersionIsPublished ? "Published" : "Draft";

            if (CurrentVersionValidTo is not null)
            {
                return $"Version {CurrentVersionNumber} ({statusWord}) Valid from {CurrentVersionValidFrom:dd/MM/yyyy} to {CurrentVersionValidTo:dd/MM/yyyy}";
            }

            var validationStatusSuffix = !CurrentVersionIsPublished && !string.IsNullOrEmpty(ProfileStatus)
                ? $" ({ProfileStatus})"
                : string.Empty;

            return $"Version {CurrentVersionNumber} ({statusWord}) Valid from {CurrentVersionValidFrom:dd/MM/yyyy}{validationStatusSuffix}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether the "Print full profile" link is shown, matching
    /// <c>ProfileVersionInfo.CanGetPrintVersion()</c>: the current version must not be Published, and the user must be a
    /// profile editor, a policy profile user, or hold a role on the profile.
    /// </summary>
    public bool CanPrintProfileSection { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "Summary profile report" and "Summary prioritisation
    /// report" links are shown, matching <c>ProfileVersionInfo.CanGetSummaryReport()</c>: the
    /// current version must not be Published and must have no effective-to date, and the user must be a profile editor,
    /// a policy profile user, or hold a role on the profile.
    /// </summary>
    public bool CanViewProfileReports { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user can edit at least one section of the current version: the version must be a
    /// current (no effective-to date) Draft, and the user must be a profile editor or an author assigned to a section. Which
    /// sections they can edit is <see cref="CanEditAllSections"/> and <see cref="EditableSectionIds"/>.
    /// </summary>
    public bool CanEditProfileSection { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user can edit every section of the current version: a profile editor, on the
    /// current draft.
    /// </summary>
    public bool CanEditAllSections { get; init; }

    /// <summary>
    /// Gets the sections of the current draft that an external author is assigned to, and so may edit. Empty for a profile
    /// editor, who may edit every section (<see cref="CanEditAllSections"/>), and for anyone who may edit none.
    /// </summary>
    public IReadOnlyList<Guid> EditableSectionIds { get; init; } = [];

    /// <summary>Gets a value indicating whether the user can edit the author review's frequency, next date and status.</summary>
    public bool CanEditAuthorReviewDates { get; init; }

    /// <summary>Gets a value indicating whether the user can edit the VA review's frequency, next date and status.</summary>
    public bool CanEditVaReviewDates { get; init; }

    /// <summary>Gets a value indicating whether the user can edit the policy review's frequency, next date and status.</summary>
    public bool CanEditPolicyReviewDates { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "Clear" button is shown, matching
    /// <c>ProfileVersionSection.CanClearProfileVersionSection()</c>: the current version must be a
    /// current (no effective-to date) Draft, and the user must be a profile editor.
    /// </summary>
    public bool CanClearProfileSection { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user can add, edit and delete review comments: a profile editor, on the current
    /// draft. Anyone who can read the comments (<see cref="CanViewReviewComments"/>) reads them.
    /// </summary>
    public bool CanAddReviewComments { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user can read the profile's notes (its "Profile references"): a profile editor, a
    /// policy profile user, or an external user who holds a role on the profile.
    /// </summary>
    public bool CanViewNotes { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "Review comments" tab is shown, matching
    /// <c>ReviewCommentList.CanGetReviewCommentList()</c>: the current version must be Draft, and
    /// the user must be a profile editor, a policy profile user, or hold a role on the profile.
    /// </summary>
    public bool CanViewReviewComments { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "Contributions" tab is shown, matching
    /// <c>ProfileContributionInfoList.CanGetProfileContributionInfoList()</c>: the current version
    /// must be Draft, and the user must be a profile editor, a policy profile user, or hold a role on the profile.
    /// </summary>
    public bool CanViewContributions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "All Revision dates" tab is shown, matching
    /// <c>ProfileVersionSection.CanGetRevisionInformation()</c>: the current version must be
    /// Draft, and the user must be a profile editor, a policy profile user, or hold a role on the profile.
    /// </summary>
    public bool CanViewAllRevisionDates { get; init; }
}
