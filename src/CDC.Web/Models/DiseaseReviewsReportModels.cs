using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>Outcome of asking CDC.Api for the disease reviews report data.</summary>
public enum DiseaseReviewsReportOutcome
{
    /// <summary>The report data was returned.</summary>
    Success,

    /// <summary>The user may not run the disease reviews report.</summary>
    Forbidden,

    /// <summary>The request failed for any other reason.</summary>
    Error
}

/// <summary>Result of <c>GET /api/profile-reports/disease-reviews</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Report">The report data when <paramref name="Outcome"/> is <see cref="DiseaseReviewsReportOutcome.Success"/>.</param>
public sealed record DiseaseReviewsReportResult(DiseaseReviewsReportOutcome Outcome, DiseaseReviewsReportDto? Report = null);

/// <summary>The disease reviews report as laid out for the PDF.</summary>
/// <param name="Description">The description typed on the form, which may hold italic markup.</param>
/// <param name="DateRange">The line stating whose reviews are due, and between which dates.</param>
/// <param name="Profiles">One table per profile version with a review due, in the order CDC.Api returned them.</param>
public sealed record DiseaseReviewsReportViewModel(string Description, string DateRange, IReadOnlyList<DiseaseReviewsReportProfileView> Profiles);

/// <summary>One profile version's table in the report.</summary>
/// <param name="Name">The profile's title and version, for example "Anthrax v1.0". The title may hold italic markup.</param>
/// <param name="Sections">The sections with a review due, in section number order.</param>
public sealed record DiseaseReviewsReportProfileView(string Name, IReadOnlyList<DiseaseReviewsReportSectionView> Sections);

/// <summary>One section's row in a profile's table.</summary>
/// <param name="Section">The section's number and name, for example "1: Summary".</param>
/// <param name="PolicyReviewDue">The next policy review date as displayed; empty when none is set.</param>
/// <param name="AuthorReviewDue">The next author review date as displayed; empty when none is set.</param>
/// <param name="VaReviewDue">The next VA review date as displayed; empty when none is set.</param>
public sealed record DiseaseReviewsReportSectionView(string Section, string PolicyReviewDue, string AuthorReviewDue, string VaReviewDue);
