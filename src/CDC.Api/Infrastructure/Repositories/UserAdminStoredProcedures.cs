namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the user administration
/// feature. These are the same procedures the legacy
/// <c>Profiles.DataAccess.Sql.UserMaintenanceService</c> called.
/// </summary>
public static class UserAdminStoredProcedures
{
    /// <summary>Returns every internal (global) user account, ordered by full name.</summary>
    public const string GetAllGlobalUsers = "spgaGlobalUser";

    /// <summary>
    /// Returns the external (single sign-on) user maintenance data as four result sets; only
    /// the first, the user list, is read here.
    /// </summary>
    public const string GetAllSsoUserMaintenance = "spgaSsoUserMaintenance";

    /// <summary>Inserts or updates a global user. Checks <c>@LastUpdated</c> and outputs the new row version.</summary>
    public const string UpsertGlobalUser = "spiGlobalUser";

    /// <summary>Inserts or updates an external user, keyed on <c>@SsoUserId</c>.</summary>
    public const string UpsertSsoUser = "spiSsoUser";
}
