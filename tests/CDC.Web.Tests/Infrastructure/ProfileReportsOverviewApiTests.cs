using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileReportsOverviewApiTests
{
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid ReportId = Guid.NewGuid();

    [Fact]
    public async Task GetOverviewAsync_ReturnsTheOverview_OnSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(new ProfileReportsOverviewDto { ProfileVersionId = ProfileVersionId, VersionLabel = "1.2" }));

        var result = await CreateService(handler).GetOverviewAsync(ProfileVersionId);

        Assert.Equal(ProfileReportsOutcome.Success, result.Outcome);
        Assert.Equal("1.2", result.Overview!.VersionLabel);
        Assert.Equal($"/api/profile-reports/overview/{ProfileVersionId}", handler.RequestPath);
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        var result = await CreateService(handler).GetOverviewAsync(ProfileVersionId);

        Assert.Equal(ProfileReportsOutcome.Error, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ProfileReportsOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ProfileReportsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, ProfileReportsOutcome.Error)]
    public async Task GetOverviewAsync_MapsFailureStatusCodes(HttpStatusCode status, ProfileReportsOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetOverviewAsync(ProfileVersionId);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task GetContentAsync_ReturnsTheContent_OnSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(ReportContent.Create([])));

        var result = await CreateService(handler).GetContentAsync(ProfileVersionId);

        Assert.Equal(ProfileReportsOutcome.Success, result.Outcome);
        Assert.Equal("1.2", result.Content!.VersionLabel);
        Assert.Equal($"/api/profile-reports/content/{ProfileVersionId}", handler.RequestPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ProfileReportsOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ProfileReportsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, ProfileReportsOutcome.Error)]
    public async Task GetContentAsync_MapsFailureStatusCodes(HttpStatusCode status, ProfileReportsOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetContentAsync(ProfileVersionId);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetContentAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(ProfileReportsOutcome.Error, (await CreateService(handler).GetContentAsync(ProfileVersionId)).Outcome);
    }

    [Fact]
    public async Task GetPrioritisationScoresAsync_ReturnsTheScores_OnSuccess()
    {
        var scores = new ProfilePrioritisationScoresDto { CategoryScores = [new ProfileCategoryScoreDto { CategoryName = "Welfare", Rank = 3 }] };
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(scores));

        var result = await CreateService(handler).GetPrioritisationScoresAsync(ProfileVersionId);

        Assert.Equal(ProfileReportsOutcome.Success, result.Outcome);
        Assert.Equal(3, result.Scores!.CategoryScores.Single().Rank);
        Assert.Equal($"/api/profile-reports/prioritisation/{ProfileVersionId}", handler.RequestPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ProfileReportsOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ProfileReportsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, ProfileReportsOutcome.Error)]
    public async Task GetPrioritisationScoresAsync_MapsFailureStatusCodes(HttpStatusCode status, ProfileReportsOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetPrioritisationScoresAsync(ProfileVersionId);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Scores);
    }

    [Fact]
    public async Task GetPrioritisationScoresAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(ProfileReportsOutcome.Error, (await CreateService(handler).GetPrioritisationScoresAsync(ProfileVersionId)).Outcome);
    }

    [Fact]
    public async Task GetStoredReportAsync_ReturnsThePdfBytes()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(new { reportData = new byte[] { 37, 80, 68, 70 } }));

        var pdf = await CreateService(handler).GetStoredReportAsync(ProfileVersionId, ReportId);

        Assert.Equal(new byte[] { 37, 80, 68, 70 }, pdf);
        Assert.Equal($"/api/profile-reports/{ReportId}", handler.RequestPath);
        Assert.Equal($"?profileVersionId={ProfileVersionId}", handler.RequestQuery);
    }

    [Theory]
    [InlineData("{\"reportData\":\"\"}")]
    [InlineData("{\"reportData\":null}")]
    [InlineData("null")]
    public async Task GetStoredReportAsync_ReturnsNull_WhenThereIsNoData(string body)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Null(await CreateService(handler).GetStoredReportAsync(ProfileVersionId, ReportId));
    }

    [Fact]
    public async Task GetStoredReportAsync_ReturnsNull_WhenTheApiHasNoReport()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound, new StringContent(string.Empty));

        Assert.Null(await CreateService(handler).GetStoredReportAsync(ProfileVersionId, ReportId));
    }

    [Fact]
    public async Task GetStoredReportAsync_Throws_OnAnyOtherFailure()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, new StringContent(string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateService(handler).GetStoredReportAsync(ProfileVersionId, ReportId));
    }

    [Fact]
    public async Task SaveReportAsync_PostsTheReportAndReturnsSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        var outcome = await CreateService(handler).SaveReportAsync(ProfileVersionId, ReportId, "SummaryProfileGUID", [1, 2, 3]);

        Assert.Equal(ProfileReportsOutcome.Success, outcome);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/profile-reports", handler.RequestPath);
        Assert.Contains(ProfileVersionId.ToString(), handler.RequestBody);
        Assert.Contains(ReportId.ToString(), handler.RequestBody);
        Assert.Contains("SummaryProfileGUID", handler.RequestBody);
        Assert.Contains("AQID", handler.RequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ProfileReportsOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ProfileReportsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest, ProfileReportsOutcome.Error)]
    public async Task SaveReportAsync_MapsFailureStatusCodes(HttpStatusCode status, ProfileReportsOutcome expected)
    {
        var handler = new RecordingHandler(status, new StringContent(string.Empty));

        Assert.Equal(expected, await CreateService(handler).SaveReportAsync(ProfileVersionId, ReportId, "x", [1]));
    }

    private static ProfileReportsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? RequestPath { get; private set; }

        public string? RequestQuery { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestPath = request.RequestUri!.AbsolutePath;
            RequestQuery = request.RequestUri.Query;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }
}

public class ProfileReportGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_DispatchesToTheBuilderForTheReportName()
    {
        var versionId = Guid.NewGuid();
        var summary = new StubBuilder("SummaryProfileGUID", [1]);
        var qa = new StubBuilder("QABriefGUID", [2]);
        var generator = new ProfileReportGenerator([summary, qa]);

        var pdf = await generator.GenerateAsync("QABriefGUID", versionId, CancellationToken.None);

        Assert.Equal(new byte[] { 2 }, pdf);
        Assert.Equal(versionId, qa.RequestedProfileVersionId);
        Assert.Null(summary.RequestedProfileVersionId);
    }

    [Fact]
    public void CanGenerate_IsTrueOnlyForRegisteredReportNames()
    {
        var generator = new ProfileReportGenerator([new StubBuilder("SummaryProfileGUID", [1])]);

        Assert.True(generator.CanGenerate("SummaryProfileGUID"));
        Assert.False(generator.CanGenerate("summaryprofileguid"));
        Assert.False(generator.CanGenerate("FullProfileGUID"));
    }

    [Fact]
    public async Task GenerateAsync_Throws_ForAnUnsupportedReportName()
    {
        var generator = new ProfileReportGenerator([]);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => generator.GenerateAsync("Nope", Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("The report type 'Nope' is not supported", exception.Message);
    }

    private sealed class StubBuilder(string reportName, byte[] pdf) : IProfileReportPdfBuilder
    {
        public string ReportName => reportName;

        public Guid? RequestedProfileVersionId { get; private set; }

        public Task<byte[]> BuildAsync(Guid profileVersionId, CancellationToken cancellationToken)
        {
            RequestedProfileVersionId = profileVersionId;

            return Task.FromResult(pdf);
        }
    }
}

public class PdfFileNameTests
{
    [Theory]
    [InlineData("Anthrax_Summary profile", "Anthrax_Summary profile.pdf")]
    [InlineData("Foot/Mouth: A*B", "Foot_Mouth_ A_B.pdf")]
    [InlineData("  ", "Report.pdf")]
    public void Build_ReplacesInvalidCharactersAndAddsTheExtension(string name, string expected) =>
        Assert.Equal(expected, PdfFileName.Build(name));
}
