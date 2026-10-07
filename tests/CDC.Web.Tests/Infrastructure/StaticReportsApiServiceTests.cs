using System.Net;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class StaticReportsApiServiceTests
{
    [Fact]
    public async Task GetCurrentAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "22222222-2222-2222-2222-222222222222",
                "staticReportId": "11111111-1111-1111-1111-111111111111",
                "title": "Help using D2R2 guidance",
                "versionMajor": 1,
                "effectiveDateFrom": "2026-09-22T00:00:00Z",
                "effectiveDateTo": null,
                "isCurrent": true,
                "isUserManual": true,
                "isPublic": false,
                "fileSize": 4096
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var reports = await service.GetCurrentAsync(true);

        var item = Assert.Single(reports);
        Assert.Equal("Help using D2R2 guidance", item.Title);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var reports = await service.GetCurrentAsync(true);

        Assert.Empty(reports);
    }

    [Fact]
    public async Task GetHistoryAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [ { "id": "22222222-2222-2222-2222-222222222222", "title": "Help using D2R2 guidance", "versionMajor": 1 } ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var history = await service.GetHistoryAsync(Guid.NewGuid());

        Assert.Single(history);
    }

    [Fact]
    public async Task GetDataAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var data = await service.GetDataAsync(Guid.NewGuid());

        Assert.Null(data);
    }

    [Fact]
    public async Task GetDataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            { "pdfData": "AQID", "title": "Help using D2R2 guidance" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var data = await service.GetDataAsync(Guid.NewGuid());

        Assert.NotNull(data);
        Assert.Equal("Help using D2R2 guidance", data!.Title);
    }

    [Fact]
    public async Task UploadAsync_ReturnsSuccess_OnNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        var result = await service.UploadAsync(new UploadStaticReportRequestDto { Title = "Guidance", PdfData = [1, 2, 3] });

        Assert.Equal(StaticReportUpdateOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UploadAsync_ReturnsValidationFailed_OnBadRequest()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        var result = await service.UploadAsync(new UploadStaticReportRequestDto());

        Assert.Equal(StaticReportUpdateOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UploadAsync_ReturnsError_OnUnexpectedStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.UploadAsync(new UploadStaticReportRequestDto());

        Assert.Equal(StaticReportUpdateOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_OnNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(StaticReportUpdateOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsConflict_OnConflictStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(StaticReportUpdateOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsValidationFailed_OnBadRequest()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(StaticReportUpdateOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsError_OnUnexpectedStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(StaticReportUpdateOutcome.Error, result.Outcome);
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
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }
}
