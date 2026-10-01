namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the static reports feature.
/// </summary>
public static class StaticReportStoredProcedures
{
    /// <summary>Returns the current version of every report, optionally filtered to user manuals.</summary>
    public const string GetCurrent = "spgaCurrentStaticReport";

    /// <summary>Returns one version's PDF content, visibility and title.</summary>
    public const string GetData = "spgStaticReportVersionData";

    /// <summary>Uploads a new version, superseding the previous current version for that title.</summary>
    public const string Upload = "spiStaticReport";
}
