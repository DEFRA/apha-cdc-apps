namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile reports feature.
/// </summary>
public static class ProfileReportStoredProcedures
{
    /// <summary>Reads the reports available for a profile version.</summary>
    public const string GetProfileVersionReports = "spgProfileVersionReportByProfileVersionId";

    /// <summary>Reads a previously persisted report document.</summary>
    public const string GetProfileVersionReportData = "spgProfileVersionReportData";

    /// <summary>Persists a generated report document.</summary>
    public const string InsertProfileVersionReportData = "spiProfileVersionReportData";
}
