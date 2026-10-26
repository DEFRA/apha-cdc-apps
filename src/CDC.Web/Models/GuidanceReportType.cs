namespace CDC.Web.Models;

/// <summary>The four user guidance reports. The names are used in the report URL, as the legacy <c>type</c> query value was.</summary>
public enum GuidanceReportType
{
    /// <summary>Every question with its non-technical title and guidance, section by section.</summary>
    All,

    /// <summary>The questions the legacy summary profile asks, with guidance.</summary>
    SummaryProfile,

    /// <summary>The questions behind each prioritisation criterion, with guidance.</summary>
    SummaryPrioritisationReport,

    /// <summary>The question and answer sheet guidance.</summary>
    QAGuidanceReport
}
