namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the static reports and user
/// manuals feature.
/// </summary>
public static class StaticReportStoredProcedures
{
    /// <summary>Reads the current version of every static report or user manual.</summary>
    public const string GetCurrentStaticReports = "spgaCurrentStaticReport";

    /// <summary>Reads every version of one static report or user manual.</summary>
    public const string GetStaticReportHistory = "spgStaticReportHistory";

    /// <summary>Reads the stored document bytes for one version.</summary>
    public const string GetStaticReportVersionData = "spgStaticReportVersionData";

    /// <summary>Deletes one version.</summary>
    public const string DeleteStaticReportVersion = "spdStaticReportVersion";

    /// <summary>Inserts a new static report or user manual version.</summary>
    public const string UploadStaticReport = "spiStaticReport";
}
