namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing CIDM external-user and Entra ID
/// internal-user resolution.
/// </summary>
public static class UserStoredProcedures
{
    /// <summary>
    /// Returns the user row matching whichever of <c>SsoUserIdExt</c>, <c>SsoUserIdInt</c> or
    /// <c>UserName</c> is supplied, plus the user's profile/section roles (unused by CDC.Api,
    /// which only reads the first result set). The legacy app's own sign-in authorisation
    /// procedure, extended rather than duplicated - see 0062-ExtendSpgUserAuthorisationForSsoColumns.sql.
    /// </summary>
    public const string GetUserAuthorisation = "spgUserAuthorisation";

    /// <summary>Returns the user row matching an email address, if any.</summary>
    public const string GetByEmailAddress = "spgUserByEmailAddress";

    /// <summary>Backfills SsoUserIdExt on a row previously matched by email.</summary>
    public const string UpdateSsoUserIdExt = "spuUserSsoUserIdExt";

    /// <summary>Inserts a brand-new external user row.</summary>
    public const string CreateExternalUser = "spiExternalUser";

    /// <summary>Backfills SsoUserIdInt on a row previously matched by user name.</summary>
    public const string UpdateSsoUserIdInt = "spuUserSsoUserIdInt";
}
