using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the profile report endpoints on CDC.Api.</summary>
public interface IProfileReportsApiService
{
    /// <summary>Calls <c>GET /api/profile-reports/contributions/{profileId}</c>.</summary>
    /// <param name="profileId">The profile to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report data, or why it is not available.</returns>
    Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-reports/disease-reviews</c> for the profile sections whose reviews fall due in a date range.</summary>
    /// <param name="fromDate">The first day of the range.</param>
    /// <param name="toDate">The last day of the range.</param>
    /// <param name="authorId">The user the sections are assigned to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report data, or why it is not available.</returns>
    Task<DiseaseReviewsReportResult> GetDiseaseReviewsReportAsync(DateTime fromDate, DateTime toDate, Guid authorId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-reports/overview/{profileVersionId}</c>.</summary>
    /// <param name="profileVersionId">The profile version whose reports are listed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The overview, or why it is not available.</returns>
    Task<ProfileReportsOverviewResult> GetOverviewAsync(Guid profileVersionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-reports/content/{profileVersionId}</c> for the content the report PDFs print.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The content, or why it is not available.</returns>
    Task<ProfileReportContentResult> GetContentAsync(Guid profileVersionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-reports/prioritisation/{profileVersionId}</c> for the scores the summary prioritisation report prints.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The scores, or why they are not available.</returns>
    Task<ProfilePrioritisationScoresResult> GetPrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-reports/prioritisation/{profileVersionId}</c> to recalculate a profile version's prioritisation scores.</summary>
    /// <param name="profileVersionId">The profile version to prioritise.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the scores were recalculated.</returns>
    Task<ProfileReportsOutcome> PrioritiseProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-reports/guidance</c> for the guidance the bespoke report prints under its guidance headings.</summary>
    /// <param name="questionIds">The questions whose guidance is wanted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The guidance, or why it is not available.</returns>
    Task<ProfileQuestionGuidanceResult> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-reports/{reportId}</c> for a PDF stored by an earlier generation.</summary>
    /// <param name="profileVersionId">The profile version the report covers.</param>
    /// <param name="reportId">The report to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored PDF, or <see langword="null"/> when none has been generated.</returns>
    Task<byte[]?> GetStoredReportAsync(Guid profileVersionId, Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-reports</c> to store a generated PDF.</summary>
    /// <param name="profileVersionId">The profile version the report covers.</param>
    /// <param name="reportId">The report being stored.</param>
    /// <param name="reportName">The report's internal name.</param>
    /// <param name="pdf">The generated PDF.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the PDF was stored.</returns>
    Task<ProfileReportsOutcome> SaveReportAsync(
        Guid profileVersionId,
        Guid reportId,
        string reportName,
        byte[] pdf,
        CancellationToken cancellationToken = default);
}
