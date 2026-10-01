using System.Net;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class StaticReportsApiServiceTests
{
    [Fact]
    public async Task GetCurrentAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "c80b8e93-21d2-453a-b0e0-3f522d03971d",
                "staticReportId": "65fe96f2-ea67-4618-9e45-0af0026da1ef",
                "title": "D2R2 Quality Statement",
                "versionMajor": 1,
                "effectiveDateFrom": "2024-04-26T18:21:00",
                "effectiveDateTo": null,
                "isCurrent": true,
                "isUserManual": true,
                "isPublic": false,
                "fileSize": 225133
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var reports = await service.GetCurrentAsync(isUserManual: true);

        var item = Assert.Single(reports);
        Assert.Equal("D2R2 Quality Statement", item.Title);
        Assert.True(item.IsCurrent);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var reports = await service.GetCurrentAsync(isUserManual: false);

        Assert.Empty(reports);
    }

    [Fact]
    public async Task GetCurrentAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetCurrentAsync(isUserManual: false));
    }

    [Fact]
    public async Task GetDataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            { "pdfData": "JVBERi0=", "title": "D2R2 Quality Statement" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var data = await service.GetDataAsync(Guid.NewGuid());

        Assert.NotNull(data);
        Assert.Equal("D2R2 Quality Statement", data!.Title);
        Assert.NotEmpty(data.PdfData);
    }

    [Fact]
    public async Task GetDataAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var data = await service.GetDataAsync(Guid.NewGuid());

        Assert.Null(data);
    }

    [Fact]
    public async Task GetDataAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetDataAsync(Guid.NewGuid()));
    }

    private static StaticReportsApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new StaticReportsApiService(httpClient);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
