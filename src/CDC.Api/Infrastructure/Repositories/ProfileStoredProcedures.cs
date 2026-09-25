namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile search feature.
/// </summary>
public static class ProfileStoredProcedures
{
    /// <summary>Returns every profile plus its scenarios, versions and affected species, as four result sets.</summary>
    public const string GetAllProfiles = "spgaProfile";
}
