namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing CIDM external-user and Entra ID
/// internal-user resolution.
/// </summary>
public static class UserStoredProcedures
{
    /// <summary>Returns the user row matching a CIDM 'sub' claim, if any.</summary>
    public const string GetBySsoUserIdExt = "spgUserBySsoUserIdExt";

    /// <summary>Returns the user row matching an email address, if any.</summary>
    public const string GetByEmailAddress = "spgUserByEmailAddress";

    /// <summary>Backfills SsoUserIdExt on a row previously matched by email.</summary>
    public const string UpdateSsoUserIdExt = "spuUserSsoUserIdExt";

    /// <summary>Inserts a brand-new external user row.</summary>
    public const string CreateExternalUser = "spiExternalUser";

    /// <summary>Returns the user row matching an Entra ID 'oid' claim, if any.</summary>
    public const string GetBySsoUserIdInt = "spgUserBySsoUserIdInt";

    /// <summary>Returns the user row matching a user name, if any.</summary>
    public const string GetByUserName = "spgUserByUserName";

    /// <summary>Backfills SsoUserIdInt on a row previously matched by user name.</summary>
    public const string UpdateSsoUserIdInt = "spuUserSsoUserIdInt";
}
