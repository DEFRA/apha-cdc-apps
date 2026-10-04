namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing CIDM external-user resolution.
/// </summary>
public static class UserStoredProcedures
{
    /// <summary>Returns the user row matching a CIDM 'sub' claim, if any.</summary>
    public const string GetByCidmSsoId = "spgUserByCidmSsoId";

    /// <summary>Returns the user row matching an email address, if any.</summary>
    public const string GetByEmailAddress = "spgUserByEmailAddress";

    /// <summary>Backfills CidmSsoId on a row previously matched by email.</summary>
    public const string UpdateCidmSsoId = "spuUserCidmSsoId";

    /// <summary>Inserts a brand-new external user row.</summary>
    public const string CreateExternalUser = "spiExternalUser";
}
