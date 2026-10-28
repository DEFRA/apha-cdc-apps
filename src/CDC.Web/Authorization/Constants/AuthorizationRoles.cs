namespace CDC.Web.Authorization.Constants;

/// <summary>
/// Role names used by the authorization framework: the two global roles an internal (Entra ID) user can hold, and the
/// service role that is denied everything. They mirror the legacy <c>ProfilesIdentity</c> flags <c>IsProfileEditor</c>,
/// <c>IsPolicyProfileUser</c> and <c>IsUserManagementSystem</c>. <see cref="InternalRoleClaimsTransformation"/> maps an
/// internal user's permission claims onto them.
/// </summary>
/// <remarks>
/// An external (CIDM) user holds no role here. Their permissions are defined at profile level - technical, policy or VA
/// author or reviewer on a given profile - and CDC.Api applies them from the database on every call, so nothing in
/// CDC.Web can grant them a global role.
/// </remarks>
public static class AuthorizationRoles
{
    public const string ProfileEditor = nameof(ProfileEditor);
    public const string PolicyProfileUser = nameof(PolicyProfileUser);
    public const string UserManagementSystem = nameof(UserManagementSystem);
}
