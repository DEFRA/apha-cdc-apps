namespace CDC.Web.Models;

/// <summary>
/// The two summary reports a user can run on a profile's current draft, from the "Profile reports" links beside the profile. The names
/// are used in the report URL, as the legacy <c>ShowDraftSummaryProfileReport.aspx</c> and <c>ShowDraftSummaryPrioritisationReport.aspx</c>
/// pages were.
/// </summary>
public enum DraftSummaryReport
{
    /// <summary>The summary profile report: a selection of the profile's questions under ten headings.</summary>
    SummaryProfile,

    /// <summary>The summary prioritisation report: the profiled species' prioritisation ranks, with the criteria scores behind them.</summary>
    SummaryPrioritisation
}
