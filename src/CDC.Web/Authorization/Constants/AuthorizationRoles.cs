namespace CDC.Web.Authorization.Constants;

/// <summary>
/// Role names used by the authorization framework, mirroring the legacy <c>ProfilesIdentity</c>
/// role flags (<c>IsProfileEditor</c>, <c>IsPolicyProfileUser</c>, <c>IsContributor</c>,
/// <c>IsReviewer</c>, <c>IsUserManagementSystem</c>). These are plain role names, not Entra ID
/// group/claim identifiers - the future claims transformation that replaces
/// <see cref="Services.CurrentUserService"/> is responsible for mapping real identity provider
/// claims onto these same role names, so no policy or handler needs to change.
/// </summary>
public static class AuthorizationRoles
{
    public const string ProfileEditor = nameof(ProfileEditor);
    public const string PolicyProfileUser = nameof(PolicyProfileUser);
    public const string Contributor = nameof(Contributor);
    public const string Reviewer = nameof(Reviewer);
    public const string UserManagementSystem = nameof(UserManagementSystem);
}
