namespace CDC.Web.Authorization.Constants;

/// <summary>
/// Role names used by the authorization framework, mirroring the legacy <c>ProfilesIdentity</c>
/// role flags (<c>IsProfileEditor</c>, <c>IsPolicyProfileUser</c>, <c>IsContributor</c>,
/// <c>IsReviewer</c>, <c>IsUserManagementSystem</c>). These are plain role names, not Entra ID
/// group/claim identifiers - <see cref="InternalRoleClaimsTransformation"/> (internal users) and the CIDM
/// sign-in events (external users) map the signed-in user's permissions onto these same role names, so
/// no policy or handler needs to know which identity provider was used.
/// </summary>
public static class AuthorizationRoles
{
    public const string ProfileEditor = nameof(ProfileEditor);
    public const string PolicyProfileUser = nameof(PolicyProfileUser);
    public const string Contributor = nameof(Contributor);
    public const string Reviewer = nameof(Reviewer);
    public const string UserManagementSystem = nameof(UserManagementSystem);
}
