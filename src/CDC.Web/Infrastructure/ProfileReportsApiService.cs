using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Default <see cref="IProfileReportsApiService"/>.</summary>
/// <param name="httpClient">The CDC.Api client.</param>
public sealed class ProfileReportsApiService(HttpClient httpClient) : IProfileReportsApiService
{
    private const string BasePath = "/api/profile-reports";

    /// <inheritdoc />
    public async Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/contributions/{profileId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var report = await response.Content.ReadFromJsonAsync<ContributionsReportDto>(cancellationToken);

            return report is null
                ? new ContributionsReportResult(ContributionsReportOutcome.Error)
                : new ContributionsReportResult(ContributionsReportOutcome.Success, report);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new ContributionsReportResult(ContributionsReportOutcome.NotFound),
            HttpStatusCode.Forbidden => new ContributionsReportResult(ContributionsReportOutcome.Forbidden),
            _ => new ContributionsReportResult(ContributionsReportOutcome.Error)
        };
    }

    /// <inheritdoc />
    public async Task<ProfileReportsOverviewResult> GetOverviewAsync(Guid profileVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/overview/{profileVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var overview = await response.Content.ReadFromJsonAsync<ProfileReportsOverviewDto>(cancellationToken);

            return overview is null
                ? new ProfileReportsOverviewResult(ProfileReportsOutcome.Error)
                : new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, overview);
        }

        return new ProfileReportsOverviewResult(ToOutcome(response.StatusCode));
    }

    /// <inheritdoc />
    public async Task<ProfileReportContentResult> GetContentAsync(Guid profileVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/content/{profileVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<ProfileReportContentDto>(cancellationToken);

            return content is null
                ? new ProfileReportContentResult(ProfileReportsOutcome.Error)
                : new ProfileReportContentResult(ProfileReportsOutcome.Success, content);
        }

        return new ProfileReportContentResult(ToOutcome(response.StatusCode));
    }

    /// <inheritdoc />
    public async Task<ProfilePrioritisationScoresResult> GetPrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/prioritisation/{profileVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var scores = await response.Content.ReadFromJsonAsync<ProfilePrioritisationScoresDto>(cancellationToken);

            return scores is null
                ? new ProfilePrioritisationScoresResult(ProfileReportsOutcome.Error)
                : new ProfilePrioritisationScoresResult(ProfileReportsOutcome.Success, scores);
        }

        return new ProfilePrioritisationScoresResult(ToOutcome(response.StatusCode));
    }

    /// <inheritdoc />
    public async Task<ProfileQuestionGuidanceResult> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"{BasePath}/guidance",
            new ProfileQuestionGuidanceRequest { QuestionIds = questionIds },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var guidance = await response.Content.ReadFromJsonAsync<List<ProfileQuestionGuidanceDto>>(cancellationToken);

            return guidance is null
                ? new ProfileQuestionGuidanceResult(ProfileReportsOutcome.Error)
                : new ProfileQuestionGuidanceResult(ProfileReportsOutcome.Success, guidance);
        }

        return new ProfileQuestionGuidanceResult(ToOutcome(response.StatusCode));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetStoredReportAsync(Guid profileVersionId, Guid reportId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/{reportId}?profileVersionId={profileVersionId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var stored = await response.Content.ReadFromJsonAsync<StoredReport>(cancellationToken);

        return stored?.ReportData is { Length: > 0 } data ? data : null;
    }

    /// <inheritdoc />
    public async Task<ProfileReportsOutcome> SaveReportAsync(
        Guid profileVersionId,
        Guid reportId,
        string reportName,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            BasePath,
            new { profileVersionId, profileReportId = reportId, reportName, reportData = pdf },
            cancellationToken);

        return response.IsSuccessStatusCode ? ProfileReportsOutcome.Success : ToOutcome(response.StatusCode);
    }

    private static ProfileReportsOutcome ToOutcome(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => ProfileReportsOutcome.NotFound,
        HttpStatusCode.Forbidden => ProfileReportsOutcome.Forbidden,
        _ => ProfileReportsOutcome.Error
    };

    private sealed record StoredReport(byte[]? ReportData);
}
