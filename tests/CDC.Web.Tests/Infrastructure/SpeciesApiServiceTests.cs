using System.Net;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class SpeciesApiServiceTests
{
    [Fact]
    public async Task GetAllSpeciesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "parentId": "00000000-0000-0000-0000-000000000000",
                "description": "Cattle",
                "isActive": true,
                "isInUse": true
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var species = await service.GetAllSpeciesAsync();

        var item = Assert.Single(species);
        Assert.Equal("Cattle", item.Description);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task GetAllSpeciesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var species = await service.GetAllSpeciesAsync();

        Assert.Empty(species);
    }

    [Fact]
    public async Task GetAllSpeciesAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAllSpeciesAsync());
    }

    [Fact]
    public async Task GetSpeciesDetailAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "name": "Cattle",
              "parentId": "00000000-0000-0000-0000-000000000000",
              "parentName": "",
              "isActive": true,
              "isInUse": true,
              "childCount": 0,
              "activeChildCount": 0,
              "lastUpdated": "AAAAAAAAAAE="
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var detail = await service.GetSpeciesDetailAsync(Guid.NewGuid());

        Assert.NotNull(detail);
        Assert.Equal("Cattle", detail!.Name);
    }

    [Fact]
    public async Task GetSpeciesDetailAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var detail = await service.GetSpeciesDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetSpeciesValidParentsAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [ { "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "name": "Cattle" } ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var validParents = await service.GetSpeciesValidParentsAsync(Guid.NewGuid());

        var item = Assert.Single(validParents);
        Assert.Equal("Cattle", item.Name);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsSuccess_OnOk()
    {
        const string json = """
            { "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "lastUpdated": "AAAAAAAAAAI=" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsConflict_OnHttp409()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsValidationFailed_OnHttp400()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsError_OnUnexpectedStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetSpeciesAuditTrailAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "oldName": "Dairy cattle",
                "newName": "Dairy",
                "oldParent": "Cattle",
                "newParent": "Cattle",
                "changedBy": "a.user",
                "logDate": "2026-01-01T00:00:00",
                "reasonForChange": "Simplifying the name"
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var entries = await service.GetSpeciesAuditTrailAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("Dairy", entry.NewName);
    }

    private static SpeciesApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new SpeciesApiService(httpClient);
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
