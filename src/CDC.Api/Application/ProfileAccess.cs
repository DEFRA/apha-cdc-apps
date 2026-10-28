using CDC.Api.Domain.Entities;

namespace CDC.Api.Application;

/// <summary>
/// The rules that decide what each kind of user may do with a profile, in one place. They replace the legacy
/// <c>CanXxx()</c> methods, which read the user's roles from <c>ProfilesIdentity</c>, and follow the role matrix.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>The profile editor (an internal user) can do everything.</description></item>
/// <item><description>The policy profile user (an internal user) can read every draft and run every report, but change nothing.</description></item>
/// <item><description>A contributor (an author: technical, policy or VA) can read the drafts of the profiles they hold a role on,
/// edit the sections assigned to them, change the notes, and write review comments.</description></item>
/// <item><description>A reviewer can read the drafts of the profiles they hold a role on and write review comments, and change
/// nothing else.</description></item>
/// </list>
/// Published versions can be read by anyone signed in. Only the current draft can be changed. A user holds roles on profiles only
/// when they hold no global role, and an external user never holds a global role: <see cref="IUserContext"/> already enforces that.
/// </remarks>
public static class ProfileAccess
{
    /// <summary>Gets whether the user is a profile editor.</summary>
    /// <param name="user">The acting user.</param>
    /// <returns><see langword="true"/> for an internal user holding the profile editor role.</returns>
    public static bool IsEditor(IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsProfileEditor && !user.IsUserManagementSystem;
    }

    /// <summary>Gets whether the user is a policy profile user.</summary>
    /// <param name="user">The acting user.</param>
    /// <returns><see langword="true"/> for an internal user holding the policy profile user role.</returns>
    public static bool IsPolicyUser(IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsPolicyProfileUser && !user.IsUserManagementSystem;
    }

    /// <summary>Gets whether the user is a profile editor or a policy profile user: a global user with no limit to one profile.</summary>
    /// <param name="user">The acting user.</param>
    /// <returns><see langword="true"/> for a global user.</returns>
    public static bool IsGlobalUser(IUserContext user) => IsEditor(user) || IsPolicyUser(user);

    /// <summary>Gets whether the user holds a role on a profile.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="profileId">The profile.</param>
    /// <returns><see langword="true"/> for a contributor or reviewer of the profile.</returns>
    public static bool IsAssigned(IUserContext user, Guid profileId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return !user.IsUserManagementSystem && user.GetProfileRole(profileId) is not null;
    }

    /// <summary>Gets whether the user can read a profile's draft versions, sections, notes, review comments and history.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="profileId">The profile.</param>
    /// <returns><see langword="true"/> for a global user, or a user who holds a role on the profile.</returns>
    public static bool CanViewDraft(IUserContext user, Guid profileId) => IsGlobalUser(user) || IsAssigned(user, profileId);

    /// <summary>Gets whether the user can read a version: any published version, or a draft they can view.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> when the user may read the version.</returns>
    public static bool CanView(IUserContext user, ProfileVersionFacts version) => version.IsPublished || CanViewDraft(user, version.ProfileId);

    /// <summary>
    /// Gets whether the user can read the draft-only tabs (review comments, contributions and all revision dates): the version
    /// must be a draft, which need not be the current one.
    /// </summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> when the tabs may be shown.</returns>
    public static bool CanViewDraftTabs(IUserContext user, ProfileVersionFacts version) =>
        !version.IsPublished && CanViewDraft(user, version.ProfileId);

    /// <summary>Gets whether the user can edit one section's answers, references and further information.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <param name="profileSectionId">The section.</param>
    /// <returns><see langword="true"/> for a profile editor, or a contributor assigned to the section, on the current draft.</returns>
    public static bool CanEditSection(IUserContext user, ProfileVersionFacts version, Guid profileSectionId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return version.IsCurrentDraft && !user.IsUserManagementSystem
            && (user.IsProfileEditor || user.IsAssignedToSection(version.ProfileId, profileSectionId));
    }

    /// <summary>Gets whether the user can edit at least one section of the version.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a profile editor, or a contributor assigned to any section, on the current draft.</returns>
    public static bool CanEditAnySection(IUserContext user, ProfileVersionFacts version)
    {
        ArgumentNullException.ThrowIfNull(user);

        return version.IsCurrentDraft && !user.IsUserManagementSystem
            && (user.IsProfileEditor || user.GetAssignedSectionIds(version.ProfileId).Any());
    }

    /// <summary>Gets whether the user can clear a section.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a profile editor on the current draft.</returns>
    public static bool CanClearSection(IUserContext user, ProfileVersionFacts version) => version.IsCurrentDraft && IsEditor(user);

    /// <summary>Gets whether the user can add, edit and delete notes.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a profile editor, or a contributor to the profile, on the current draft.</returns>
    public static bool CanChangeNotes(IUserContext user, ProfileVersionFacts version)
    {
        ArgumentNullException.ThrowIfNull(user);

        return version.IsCurrentDraft && !user.IsUserManagementSystem
            && (user.IsProfileEditor || user.GetProfileRole(version.ProfileId) is { IsContributor: true });
    }

    /// <summary>Gets whether the user can edit the review frequencies, next review dates and statuses.</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a profile editor on the current draft.</returns>
    public static bool CanEditReviewDates(IUserContext user, ProfileVersionFacts version) => version.IsCurrentDraft && IsEditor(user);

    /// <summary>
    /// Gets whether the user can add review comments, reply to them, and edit or delete their own. Everyone who can read the
    /// comments reads them; the policy profile user can read them but not write one.
    /// </summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a profile editor, or a contributor or reviewer of the profile, on the current draft.</returns>
    public static bool CanWriteReviewComments(IUserContext user, ProfileVersionFacts version) =>
        version.IsCurrentDraft && (IsEditor(user) || IsAssigned(user, version.ProfileId));

    /// <summary>Gets whether the user can print the full profile or one section of a draft (legacy <c>CanGetPrintVersion</c>).</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a draft the user can view.</returns>
    public static bool CanPrintDraft(IUserContext user, ProfileVersionFacts version) =>
        !version.IsPublished && CanViewDraft(user, version.ProfileId);

    /// <summary>Gets whether the user can run the summary profile and summary prioritisation reports (legacy <c>CanGetSummaryReport</c>).</summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for the current draft, when the user can view it.</returns>
    public static bool CanRunDraftReports(IUserContext user, ProfileVersionFacts version) =>
        version.IsCurrentDraft && CanViewDraft(user, version.ProfileId);

    /// <summary>
    /// Gets whether the user can create and store a profile report for a version (legacy <c>CanCreateProfileReport</c>): a published
    /// version's reports are made by a profile editor, a draft's by anyone who can view it. Only the current version has reports.
    /// </summary>
    /// <param name="user">The acting user.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> when the user may create the version's reports.</returns>
    public static bool CanCreateReports(IUserContext user, ProfileVersionFacts version)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsUserManagementSystem || !version.IsCurrent)
        {
            return false;
        }

        return version.IsPublished ? user.IsProfileEditor : CanViewDraft(user, version.ProfileId);
    }

    /// <summary>
    /// Gets whether the user receives the review-due emails of one review on a profile: a profile editor receives every one, a
    /// contributor or reviewer the one their role owns, and a policy profile user none.
    /// </summary>
    /// <param name="user">The acting user.</param>
    /// <param name="profileId">The profile.</param>
    /// <param name="reviewType">The review.</param>
    /// <returns><see langword="true"/> when the user is a recipient.</returns>
    public static bool ReceivesReviewEmails(IUserContext user, Guid profileId, ReviewType reviewType)
    {
        ArgumentNullException.ThrowIfNull(user);

        return !user.IsUserManagementSystem && (user.IsProfileEditor || user.GetProfileRole(profileId)?.ReviewType == reviewType);
    }
}
