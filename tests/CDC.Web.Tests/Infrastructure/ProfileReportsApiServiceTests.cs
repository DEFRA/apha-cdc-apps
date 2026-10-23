using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileReportsApiServiceTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public async Task GetContributionsReportAsync_ReturnsTheReport_OnSuccess()
    {
        var report = new ContributionsReportDto { ProfileId = ProfileId, ProfileTitle = "Anthrax" };
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(report));

        var result = await CreateService(handler).GetContributionsReportAsync(ProfileId);

        Assert.Equal(ContributionsReportOutcome.Success, result.Outcome);
        Assert.Equal("Anthrax", result.Report!.ProfileTitle);
        Assert.Equal($"/api/profile-reports/contributions/{ProfileId}", handler.RequestPath);
    }

    [Fact]
    public async Task GetContributionsReportAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        var result = await CreateService(handler).GetContributionsReportAsync(ProfileId);

        Assert.Equal(ContributionsReportOutcome.Error, result.Outcome);
        Assert.Null(result.Report);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ContributionsReportOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ContributionsReportOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, ContributionsReportOutcome.Error)]
    [InlineData(HttpStatusCode.BadRequest, ContributionsReportOutcome.Error)]
    public async Task GetContributionsReportAsync_MapsFailureStatusCodes(HttpStatusCode status, ContributionsReportOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetContributionsReportAsync(ProfileId);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task GetDiseaseReviewsReportAsync_ReturnsTheReport_AndSendsTheFiltersAsIsoDates()
    {
        var authorId = Guid.NewGuid();
        var report = new DiseaseReviewsReportDto { Sections = [new DiseaseReviewsReportRowDto { SectionName = "Summary" }] };
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(report));

        var result = await CreateService(handler).GetDiseaseReviewsReportAsync(new DateTime(2027, 1, 5, 13, 30, 0), new DateTime(2027, 12, 31), authorId);

        Assert.Equal(DiseaseReviewsReportOutcome.Success, result.Outcome);
        Assert.Equal("Summary", Assert.Single(result.Report!.Sections).SectionName);
        Assert.Equal("/api/profile-reports/disease-reviews", handler.RequestPath);
        Assert.Equal($"?fromDate=2027-01-05&toDate=2027-12-31&authorId={authorId}", handler.RequestQuery);
    }

    [Fact]
    public async Task GetDiseaseReviewsReportAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        var result = await CreateService(handler).GetDiseaseReviewsReportAsync(new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), Guid.NewGuid());

        Assert.Equal(DiseaseReviewsReportOutcome.Error, result.Outcome);
        Assert.Null(result.Report);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, DiseaseReviewsReportOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, DiseaseReviewsReportOutcome.Error)]
    [InlineData(HttpStatusCode.BadRequest, DiseaseReviewsReportOutcome.Error)]
    [InlineData(HttpStatusCode.InternalServerError, DiseaseReviewsReportOutcome.Error)]
    public async Task GetDiseaseReviewsReportAsync_MapsFailureStatusCodes(HttpStatusCode status, DiseaseReviewsReportOutcome expected)
    {
        var service = CreateService(new RecordingHandler(status, new StringContent(string.Empty)));

        var result = await service.GetDiseaseReviewsReportAsync(new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), Guid.NewGuid());

        Assert.Equal(expected, result.Outcome);
    }

    private static ProfileReportsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        public string? RequestQuery { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri!.AbsolutePath;
            RequestQuery = request.RequestUri.Query;

            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }
    }
}
