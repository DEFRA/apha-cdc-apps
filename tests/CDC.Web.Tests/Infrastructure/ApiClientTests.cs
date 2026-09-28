using System.Net;
using System.Text;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class ApiClientTests
{
    [Fact]
    public async Task GetHealthAsync_DeserialisesTheResponseBody()
    {
        const string json = """{"status":"Healthy","uptimeSeconds":12.5,"timestampUtc":"2026-01-01T00:00:00Z"}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var health = await client.GetHealthAsync();

        Assert.NotNull(health);
        Assert.Equal("Healthy", health!.Status);
        Assert.Equal("/health", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SearchProfilesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "title": "Bovine tuberculosis",
                "status": "Published",
                "createdAtUtc": "2026-01-01T00:00:00Z",
                "modifiedAtUtc": "2026-01-02T00:00:00Z",
                "isPublic": true,
                "affectedSpecies": [],
                "publishedVersions": [],
                "draftVersions": [],
                "scenarios": []
              }
            ]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var results = await client.SearchProfilesAsync("tb", true, false, true);

        var item = Assert.Single(results);
        Assert.Equal("Bovine tuberculosis", item.Title);
    }

    [Fact]
    public async Task SearchProfilesAsync_BuildsTheQueryStringFromEveryFilter()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        await client.SearchProfilesAsync("tb", true, false, true);

        var query = handler.LastRequestUri!.Query;
        Assert.Contains("searchText=tb", query);
        Assert.Contains("displayPublished=True", query);
        Assert.Contains("displayDraft=False", query);
        Assert.Contains("displayScenarios=True", query);
    }

    [Fact]
    public async Task SearchProfilesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var results = await client.SearchProfilesAsync(null, true, false, false);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProfilesAsync_Throws_OnANonSuccessStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchProfilesAsync(null, true, false, false));
    }

    [Fact]
    public async Task GetProfileAttributesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "title": "Bovine tuberculosis",
              "lastUpdated": "AQIDBAUGBwg="
            }
            """;
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var attributes = await client.GetProfileAttributesAsync(profileId);

        Assert.NotNull(attributes);
        Assert.Equal("Bovine tuberculosis", attributes!.Title);
        Assert.Equal(8, attributes.LastUpdated.Length);
        Assert.Equal($"/api/profiles/{profileId}/attributes", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProfileAttributesAsync_ReturnsNull_WhenTheProfileDoesNotExist()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var attributes = await client.GetProfileAttributesAsync(Guid.NewGuid());

        Assert.Null(attributes);
    }

    [Fact]
    public async Task GetProfileAttributesAsync_Throws_OnANonSuccessNonNotFoundStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProfileAttributesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsSuccess_OnNoContent()
    {
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.UpdateProfileTitleAsync(profileId, "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal($"/api/profiles/{profileId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsConflict_OnHttpConflict()
    {
        const string json = """{"detail":"Another user has updated this profile."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, json));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Conflict, result.Outcome);
        Assert.Equal("Another user has updated this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsConflict_WithDefaultMessage_WhenProblemDetailHasNoDetail()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, "{}"));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Conflict, result.Outcome);
        Assert.Equal("This profile has been edited by another user. Reload the page and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsValidationFailed_OnBadRequestWithErrors()
    {
        const string json = """{"errors":{"Title":["You must enter a profile title"]}}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("You must enter a profile title", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Error, result.Outcome);
        Assert.Equal("The profile title could not be saved. Please try again.", result.ErrorMessage);
    }

    private static ApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ApiClient(httpClient);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
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
