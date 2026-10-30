using CDC.Api.Domain.Entities;

namespace CDC.Api.Application;

/// <summary>
/// The signed-in user making the request: their <c>[User].Id</c>, which kind of user they are, and the roles that
/// kind of user can hold. An internal user (<c>SsoUserIdInt</c>) holds the global profile editor or policy profile
/// user role or, when it holds neither, roles on individual profiles; an external user (<c>SsoUserIdExt</c>) holds roles
/// on individual profiles and never a global role. This keeps the legacy <c>ProfilesIdentity</c> flags (<c>IsProfileEditor</c>,
/// <c>IsUserManagementSystem</c>, <c>IsPolicyProfileUser</c>) that <c>Profile.vb</c> and
/// <c>ProfileContributorList.vb</c> used to gate actions, and adds what <c>ProfileAccess</c> needs to apply the
/// profile-level rules.
/// </summary>
/// <remarks>
/// CDC.Web forwards the signed-in user's id and <c>ActingUserMiddleware</c> reads the rest from the
/// database, so it always reflects the user's current record. A request with no known user is
/// anonymous: it has no id, no role and no kind.
/// </remarks>
public interface IUserContext
{
    /// <summary>Gets the acting user's <c>[User].Id</c>, or <see langword="null"/> when the request carries no known user.</summary>
    Guid? UserId { get; }

    /// <summary>Gets a value indicating whether the user is an internal user who can author/publish profiles.</summary>
    bool IsProfileEditor { get; }

    /// <summary>Gets a value indicating whether the user is a read-only "user management system" account.</summary>
    bool IsUserManagementSystem { get; }

    /// <summary>Gets a value indicating whether the user is an internal policy profile user, who can read every draft.</summary>
    bool IsPolicyProfileUser { get; }

    /// <summary>Gets which kind of user this is, from the sign-on identifier held on their row.</summary>
    UserKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the user is a contributor or reviewer of at least one profile (legacy
    /// <c>ProfilesIdentity.IsContributorOrReviewer</c>). A user who holds a global role holds no profile roles.
    /// </summary>
    bool HoldsAnyProfileRole { get; }

    /// <summary>Gets the role a user holds on a profile.</summary>
    /// <param name="profileId">The profile.</param>
    /// <returns>
    /// The role, or <see langword="null"/> when the user holds no role on the profile or cannot hold one: an unclassified user, or
    /// an internal user who holds a global role.
    /// </returns>
    ProfileRoleAssignment? GetProfileRole(Guid profileId);

    /// <summary>Gets whether an external author is assigned to a profile section.</summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="profileSectionId">The section.</param>
    /// <returns><see langword="true"/> when the user is an author on the profile and the section is assigned to them.</returns>
    bool IsAssignedToSection(Guid profileId, Guid profileSectionId);

    /// <summary>Gets the sections of a profile an external author is assigned to.</summary>
    /// <param name="profileId">The profile.</param>
    /// <returns>The section ids; empty when the user is not an author on the profile.</returns>
    IEnumerable<Guid> GetAssignedSectionIds(Guid profileId);
}

/// <summary>
/// The <see cref="IUserContext"/> for one request, filled in by <c>ActingUserMiddleware</c>. Until
/// it is, or when the request names no known user, the context is anonymous.
/// </summary>
public sealed class RequestUserContext : IUserContext
{
    private ActingUser? actingUser;

    /// <inheritdoc />
    public Guid? UserId => actingUser?.Id;

    /// <inheritdoc />
    public UserKind Kind => actingUser?.Kind ?? UserKind.Unclassified;

    /// <inheritdoc />
    public bool IsProfileEditor => Kind == UserKind.Internal && actingUser!.IsProfileEditor;

    /// <summary>
    /// Gets a value that is always <see langword="false"/>: a user management system account never
    /// holds the editor or policy flag (the legacy <c>ProfilesIdentity</c> forbids the combination),
    /// so it is already refused everything those flags allow.
    /// </summary>
    public bool IsUserManagementSystem => false;

    /// <inheritdoc />
    public bool IsPolicyProfileUser => Kind == UserKind.Internal && actingUser!.IsPolicyProfileUser;

    /// <inheritdoc />
    public bool HoldsAnyProfileRole => HoldsProfileRoles && actingUser!.ProfileRoles.Count > 0;

    /// <inheritdoc />
    public ProfileRoleAssignment? GetProfileRole(Guid profileId) =>
        HoldsProfileRoles ? actingUser!.ProfileRoles.FirstOrDefault(role => role.ProfileId == profileId) : null;

    // An external user, or an internal user with no global role; a global role makes the profile-level rows irrelevant.
    private bool HoldsProfileRoles => Kind switch
    {
        UserKind.External => true,
        UserKind.Internal => !IsProfileEditor && !IsPolicyProfileUser,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAssignedToSection(Guid profileId, Guid profileSectionId) =>
        GetAssignedSectionIds(profileId).Contains(profileSectionId);

    /// <inheritdoc />
    public IEnumerable<Guid> GetAssignedSectionIds(Guid profileId) =>
        GetProfileRole(profileId) is { IsContributor: true }
            ? actingUser!.SectionAssignments.Where(assignment => assignment.ProfileId == profileId).Select(assignment => assignment.ProfileSectionId)
            : [];

    /// <summary>Makes <paramref name="user"/> the acting user for the rest of the request.</summary>
    /// <param name="user">The user CDC.Web forwarded, as read from the database.</param>
    internal void SetActingUser(ActingUser user) => actingUser = user;
}

/// <summary>Helpers for operations that must be attributed to a signed-in user.</summary>
public static class UserContextExtensions
{
    /// <summary>
    /// Returns the acting user's id for an operation that records who made the change - the audit
    /// columns are foreign keys to <c>[User]</c>, so an anonymous request cannot make it.
    /// </summary>
    /// <param name="userContext">The current request's user context.</param>
    /// <returns>The acting user's <c>[User].Id</c>.</returns>
    /// <exception cref="UnauthorizedAccessException">The request has no known user; the global exception handler turns this into a 403.</exception>
    public static Guid RequireUserId(this IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return userContext.UserId ?? throw new UnauthorizedAccessException("The request has no known signed-in user.");
    }
}
