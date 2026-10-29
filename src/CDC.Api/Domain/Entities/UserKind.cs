namespace CDC.Api.Domain.Entities;

/// <summary>
/// The kind of user an account is, decided by which single sign-on identifier its <c>[dbo].[User]</c> row holds.
/// Each kind has its own source of roles: an internal user's are the global flags on the row, an external user's
/// are the roles granted on individual profiles.
/// </summary>
public enum UserKind
{
    /// <summary>The row holds neither identifier, or both, so the user holds no role at all.</summary>
    Unclassified,

    /// <summary>The row holds <c>SsoUserIdInt</c> (Microsoft Entra ID): a global user with the profile editor or policy profile user role.</summary>
    Internal,

    /// <summary>The row holds <c>SsoUserIdExt</c> (CIDM): a user whose roles are granted on individual profiles.</summary>
    External
}
