namespace CDC.Api.Application;

/// <summary>
/// The current user's profile-authoring role flags, matching the legacy <c>ProfilesIdentity</c>
/// business object (<c>IsProfileEditor</c>, <c>IsUserManagementSystem</c>, <c>IsPolicyProfileUser</c>)
/// used throughout <c>Profile.vb</c> and <c>ProfileContributorList.vb</c> to gate actions.
/// </summary>
/// <remarks>
/// Real authentication (Entra ID SAML/OIDC) has not been wired up yet - see the "Identity
/// Migration" work, which is explicitly out of scope here. <see cref="DefaultUserContext"/> is a
/// deliberate, flagged placeholder standing in for a real claims-based implementation: it must be
/// replaced (not the call sites that use it) once a signed-in user's roles are available.
/// </remarks>
public interface IUserContext
{
    /// <summary>Gets a value indicating whether the user can author/publish profiles.</summary>
    bool IsProfileEditor { get; }

    /// <summary>Gets a value indicating whether the user is a read-only "user management system" account.</summary>
    bool IsUserManagementSystem { get; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    bool IsPolicyProfileUser { get; }
}

/// <summary>
/// TEMPORARY placeholder for <see cref="IUserContext"/>: treats every request as a fully
/// privileged profile editor, matching the legacy app's default internal-user role, so the
/// Manage Profile link visibility rules can be ported and tested now without inventing new
/// authorisation behaviour. Replace with a claims-based implementation when Entra ID
/// authentication is wired up.
/// </summary>
public sealed class DefaultUserContext : IUserContext
{
    /// <inheritdoc />
    public bool IsProfileEditor => true;

    /// <inheritdoc />
    public bool IsUserManagementSystem => false;

    /// <inheritdoc />
    public bool IsPolicyProfileUser => false;
}
