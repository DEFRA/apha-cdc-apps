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

    private static ProfileReportsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri!.AbsolutePath;

            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }
    }
}
