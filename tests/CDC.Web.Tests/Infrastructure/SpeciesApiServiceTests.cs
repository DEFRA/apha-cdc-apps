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
