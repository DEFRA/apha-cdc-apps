namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile management feature.
/// </summary>
public static class ProfileManagementStoredProcedures
{
    /// <summary>Inserts a profile row. Outputs the new row version.</summary>
    public const string InsertProfile = "spiProfile";

    /// <summary>Inserts a profile version row.</summary>
    public const string InsertProfileVersion = "spiProfileVersion";

    /// <summary>Inserts one affected species row for a profile version, optionally cloning from another version.</summary>
    public const string InsertProfileVersionSpecies = "spiProfileVersionSpecies";

    /// <summary>Copies a species' trade/questionnaire data into a profile version.</summary>
    public const string UpdateProfileVersionSpeciesTradeData = "spuProfileVersionSpeciesTradeData";

    /// <summary>Updates a profile row. Checks <c>@LastUpdated</c> for optimistic concurrency and outputs the new row version.</summary>
    public const string UpdateProfile = "spuProfile";

    /// <summary>Removes one affected species row from a profile version.</summary>
    public const string DeleteProfileVersionSpecies = "spdProfileVersionSpecies";

    /// <summary>Reads a profile's attributes and affected species, as two result sets.</summary>
    public const string GetProfile = "spgProfile";

    /// <summary>
    /// Reads a profile version's summary details and its affected species, as two result sets.
    /// Used both to read new-profile defaults and to validate a version before creating a new
    /// version of it or deleting it.
    /// </summary>
    public const string GetProfileVersionInfoById = "spgProfileVersionInfoById";

    /// <summary>Reads every profile status.</summary>
    public const string GetProfileStatusTypes = "spgaProfileStatusType";

    /// <summary>Reads one species' name and active state by identifier.</summary>
    public const string GetSpeciesNameById = "spgSpeciesNameById";

    /// <summary>Deletes a profile version. Outputs the next-latest version id and whether the profile was also deleted.</summary>
    public const string DeleteProfileVersion = "spdProfileVersion";

    /// <summary>Updates a profile version's state and effective-to date.</summary>
    public const string UpdateProfileVersionCurrency = "sppProfileVersionCurrency";

    /// <summary>Toggles a profile version's public visibility flag.</summary>
    public const string UpdateProfileVersionPublicFlag = "spuProfileVersionPublicFlag";

    /// <summary>Sets a profile's status.</summary>
    public const string UpdateProfileStatus = "spuProfileStatus";

    /// <summary>Calculates prioritisation inputs for one profile version.</summary>
    public const string CalculatePrioritisation = "sppPrioritisationCalculation";

    /// <summary>Recalculates prioritisation scores across all profile versions.</summary>
    public const string CalculatePrioritisationScore = "sppPrioritisationScore";
}
