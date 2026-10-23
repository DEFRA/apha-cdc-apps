using System.Net;
using System.Net.Http.Headers;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class StaticReportDocumentApiServiceTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task GetDocumentAsync_RequestsTheVersionDocument_WithTheDownloadFlag(bool download, string expectedFlag)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Pdf) });
        var service = CreateService(handler);

        await service.GetDocumentAsync(VersionId, download);

        Assert.Equal($"/api/static-reports/versions/{VersionId}/document", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal($"?download={expectedFlag}", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsTheBytesAndContentDisposition()
    {
        var handler = new StubHandler(_ =>
        {
            var content = new ByteArrayContent(Pdf);
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "\"Report.pdf\"" };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var service = CreateService(handler);

        var document = await service.GetDocumentAsync(VersionId, download: true);

        Assert.NotNull(document);
        Assert.Equal(Pdf, document.Content);
        Assert.Contains("attachment", document.ContentDisposition);
        Assert.Contains("Report.pdf", document.ContentDisposition);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsNull_WhenTheVersionDoesNotExist()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        Assert.Null(await service.GetDocumentAsync(VersionId, download: false));
    }

    [Fact]
    public async Task GetDocumentAsync_Throws_WhenTheApiFails()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetDocumentAsync(VersionId, download: false));
    }

    private static StaticReportDocumentApiService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(respond(request));
        }
    }
}
