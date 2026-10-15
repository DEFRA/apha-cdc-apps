using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>Scriptable <see cref="IProfileReportsApiService"/> that records what it was asked for.</summary>
internal sealed class FakeProfileReportsApiService(
    ContributionsReportResult? contributions = null,
    ProfileReportsOverviewResult? overview = null,
    byte[]? storedReport = null,
    ProfileReportsOutcome saveOutcome = ProfileReportsOutcome.Success,
    Exception? throwOnContributions = null,
    Exception? throwOnOverview = null,
    Exception? throwOnStoredReport = null,
    Exception? throwOnSave = null,
    ProfileReportContentResult? content = null,
    Exception? throwOnContent = null,
    ProfilePrioritisationScoresResult? prioritisationScores = null,
    Exception? throwOnPrioritisationScores = null,
    ProfileQuestionGuidanceResult? guidance = null,
    Exception? throwOnGuidance = null) : IProfileReportsApiService
{
    /// <summary>Gets the question ids passed to the guidance call.</summary>
    public IReadOnlyList<Guid>? RequestedGuidanceIds { get; private set; }

    /// <summary>Gets the profile version passed to the content call.</summary>
    public Guid? RequestedContentVersionId { get; private set; }

    /// <summary>Gets the profile id passed to the contributions call.</summary>
    public Guid? RequestedProfileId { get; private set; }

    /// <summary>Gets the report id and name passed to the save call.</summary>
    public (Guid ReportId, string ReportName, byte[] Pdf)? Saved { get; private set; }

    public Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        RequestedProfileId = profileId;

        return throwOnContributions is not null ? throw throwOnContributions : Task.FromResult(contributions!);
    }

    public Task<ProfileReportsOverviewResult> GetOverviewAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
        throwOnOverview is not null ? throw throwOnOverview : Task.FromResult(overview!);

    public Task<ProfileReportContentResult> GetContentAsync(Guid profileVersionId, CancellationToken cancellationToken = default)
    {
        RequestedContentVersionId = profileVersionId;

        return throwOnContent is not null ? throw throwOnContent : Task.FromResult(content!);
    }

    public Task<ProfilePrioritisationScoresResult> GetPrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
        throwOnPrioritisationScores is not null ? throw throwOnPrioritisationScores : Task.FromResult(prioritisationScores!);

    public Task<byte[]?> GetStoredReportAsync(Guid profileVersionId, Guid reportId, CancellationToken cancellationToken = default) =>
        throwOnStoredReport is not null ? throw throwOnStoredReport : Task.FromResult(storedReport);

    public Task<ProfileQuestionGuidanceResult> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken = default)
    {
        RequestedGuidanceIds = questionIds;

        return throwOnGuidance is not null ? throw throwOnGuidance : Task.FromResult(guidance!);
    }

    public Task<ProfileReportsOutcome> SaveReportAsync(
        Guid profileVersionId,
        Guid reportId,
        string reportName,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        Saved = (reportId, reportName, pdf);

        return throwOnSave is not null ? throw throwOnSave : Task.FromResult(saveOutcome);
    }
}
