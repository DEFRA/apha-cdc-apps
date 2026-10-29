using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Browsers cannot reach CDC.Api, so documents must be streamed through CDC.Web.
public class StaticReportDocumentEndpointIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly string Path = $"/static-reports/versions/{VersionId}/document";

    private readonly CdcWebTestFactory _factory;

    public StaticReportDocumentEndpointIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_StreamsThePdf_WithTheApiContentDisposition()
    {
        var pdf = "%PDF-1.7 test"u8.ToArray();
        var client = await SignedInClientAsync(new StubDocuments((_, _) =>
            Task.FromResult<StaticReportDocument?>(new StaticReportDocument(pdf, "attachment; filename=\"Report.pdf\""))));

        var response = await client.GetAsync($"{Path}?download=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString());
    }

    [Fact]
    public async Task Get_PassesTheVersionIdAndDownloadFlagToTheApi()
    {
        Guid? requestedId = null;
        bool? requestedDownload = null;
        var client = await SignedInClientAsync(new StubDocuments((id, download) =>
        {
            requestedId = id;
            requestedDownload = download;

            return Task.FromResult<StaticReportDocument?>(new StaticReportDocument([1], null));
        }));

        await client.GetAsync($"{Path}?download=true");

        Assert.Equal(VersionId, requestedId);
        Assert.True(requestedDownload);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        var client = await SignedInClientAsync(new StubDocuments((_, _) => Task.FromResult<StaticReportDocument?>(null)));

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsBadGateway_WhenTheApiFails()
    {
        var client = await SignedInClientAsync(new StubDocuments((_, _) => throw new HttpRequestException("boom")));

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Get_DoesNotServeTheDocument_ToAnAnonymousUser()
    {
        var called = false;
        using var factory = WithDocuments(new StubDocuments((_, _) =>
        {
            called = true;

            return Task.FromResult<StaticReportDocument?>(new StaticReportDocument([1], null));
        }));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(Path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.False(called);
    }

    private async Task<HttpClient> SignedInClientAsync(StubDocuments documents) =>
        await WithDocuments(documents).SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: false);

    private WebApplicationFactory<Program> WithDocuments(StubDocuments documents) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStaticReportDocumentApiService>();
            services.AddSingleton<IStaticReportDocumentApiService>(documents);
        }));

    private sealed class StubDocuments(Func<Guid, bool, Task<StaticReportDocument?>> handler) : IStaticReportDocumentApiService
    {
        public Task<StaticReportDocument?> GetDocumentAsync(Guid staticReportVersionId, bool download, CancellationToken cancellationToken = default) =>
            handler(staticReportVersionId, download);
    }
}
