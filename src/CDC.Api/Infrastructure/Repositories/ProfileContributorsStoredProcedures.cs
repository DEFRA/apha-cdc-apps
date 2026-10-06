namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile contributors
/// feature. This is the same procedure the legacy <c>ProfilesLibrary.ProfileContributorList</c>
/// CSLA object fetched through.
/// </summary>
public static class ProfileContributorsStoredProcedures
{
    /// <summary>Returns a profile's contributors.</summary>
    public const string GetProfileContributorsByProfileId = "spgProfileContributorsByProfileId";

    /// <summary>Returns one contributor's editable detail and granted section permissions.</summary>
    public const string GetContributor = "spgContributor";

    /// <summary>Returns every role a contributor can hold.</summary>
    public const string GetProfileUserRoles = "spgaluProfileUserRole";

    /// <summary>Upserts a contributor's user/role row; checks the row version first.</summary>
    public const string UpsertContributor = "spiProfileContributor";

    /// <summary>Grants a single profile section permission, if not already granted.</summary>
    public const string AddSectionPermission = "spiProfileSectionUser";

    /// <summary>Revokes a single profile section permission.</summary>
    public const string RemoveSectionPermission = "spdProfileSectionUser";

    /// <summary>Looks up a global user by username, for the "Add profile contributor" lookup step.</summary>
    public const string FindUserByUsername = "spgUserAuthorisation";

    /// <summary>Removes a contributor (and their section permissions) from a profile; checks the row version first.</summary>
    public const string DeleteContributor = "spdProfileContributor";
}
