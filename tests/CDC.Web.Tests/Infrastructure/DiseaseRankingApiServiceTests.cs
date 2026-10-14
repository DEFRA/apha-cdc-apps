using System.Net;
using System.Text;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class DiseaseRankingApiServiceTests
{
    [Fact]
    public async Task GetReportOptionsAsync_DeserialisesTheOptions()
    {
        const string json = """[{"key":"terrestrial","label":"Terrestrial"},{"key":"aquatic","label":"Aquatic"}]""";
        var handler = new RecordingHandler(HttpStatusCode.OK, json);

        var options = await CreateClient(handler).GetReportOptionsAsync();

        Assert.Equal(["terrestrial", "aquatic"], options.Select(option => option.Key));
        Assert.Equal("/api/disease-ranking/report-options", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetReportOptionsAsync_ReturnsAnEmptyList_WhenTheBodyIsNull()
    {
        var options = await CreateClient(new RecordingHandler(HttpStatusCode.OK, "null")).GetReportOptionsAsync();

        Assert.Empty(options);
    }

    [Fact]
    public async Task GetReportAsync_RequestsTheReportWithItsKindAndDeserialisesIt()
    {
        const string json = """{"kind":1,"title":"Report","footerTitle":"Footer","fileName":"File","charts":[{"title":"Chart","bars":[]}]}""";
        var handler = new RecordingHandler(HttpStatusCode.OK, json);

        var report = await CreateClient(handler).GetReportAsync("terrestrial", DiseaseRankingReportKind.Ranked);

        Assert.NotNull(report);
        Assert.Equal(DiseaseRankingReportKind.Ranked, report.Kind);
        Assert.Equal("Chart", Assert.Single(report.Charts).Title);
        Assert.Equal("/api/disease-ranking/reports/terrestrial", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal("?kind=Ranked", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetReportAsync_EscapesTheKeyInThePath()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");

        await CreateClient(handler).GetReportAsync("a/b c", DiseaseRankingReportKind.Scored);

        Assert.Equal("/api/disease-ranking/reports/a%2Fb%20c", handler.LastRequestUri!.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsNull_WhenTheReportIsNotFound()
    {
        var report = await CreateClient(new RecordingHandler(HttpStatusCode.NotFound, string.Empty))
            .GetReportAsync("bogus", DiseaseRankingReportKind.Scored);

        Assert.Null(report);
    }

    [Fact]
    public async Task GetReportAsync_Throws_WhenTheApiFails()
    {
        var client = CreateClient(new RecordingHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReportAsync("terrestrial", DiseaseRankingReportKind.Scored));
    }

    private static DiseaseRankingApiService CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
