using System.Net;
using System.Text;
using System.Text.Json;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfilePropertiesApiServiceTests
{
    private static readonly Guid ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CloneVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Records the request it was sent, so a test can check the URL and body that went over the wire.
    private sealed class CapturingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private static ProfilePropertiesApiService CreateService(CapturingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private static CreateProfileRequest CreateRequest() => new()
    {
        Id = ProfileId,
        CurrentDraftProfileVersionId = Guid.NewGuid(),
        CloneProfileVersionId = CloneVersionId,
        Title = "Anthrax",
        ParentId = Guid.Empty,
        ProfileStatusId = Guid.Empty,
        AffectedSpeciesInsertList = [new AffectedSpeciesInsertDto { SpeciesId = Guid.NewGuid(), Type = "Profiled" }]
    };

    private static UpdateProfileAttributesRequest UpdateRequest() => new()
    {
        Id = ProfileId,
        Title = "Anthrax",
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    [Fact]
    public async Task GetNewProfileDefaultsAsync_RequestsTheDefaults_AndDeserialisesThem()
    {
        const string json = """
            {
              "title": "Anthrax",
              "scenarioTitle": "Wildlife",
              "parentId": "11111111-1111-1111-1111-111111111111",
              "parentTitle": "Anthrax",
              "profileStatusId": "33333333-3333-3333-3333-333333333333",
              "clonedVersionDescription": "Anthrax - v1.0 (Published) Valid from 01/01/2020",
              "clonedVersionIsWhatIfScenario": true,
              "affectedSpecies": [ { "speciesId": "44444444-4444-4444-4444-444444444444", "name": "Cattle", "type": "Profiled", "isActive": true } ]
            }
            """;
        var handler = new CapturingHandler(HttpStatusCode.OK, json);

        var defaults = await CreateService(handler).GetNewProfileDefaultsAsync(CloneVersionId, isWhatIfScenario: true);

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal($"/api/profiles/defaults?cloneProfileVersionId={CloneVersionId}&isWhatIfScenario=True", handler.Request.RequestUri!.PathAndQuery);
        Assert.NotNull(defaults);
        Assert.Equal("Wildlife", defaults.ScenarioTitle);
        Assert.Equal(ProfileId, defaults.ParentId);
        Assert.Equal("Anthrax - v1.0 (Published) Valid from 01/01/2020", defaults.ClonedVersionDescription);
        Assert.True(defaults.ClonedVersionIsWhatIfScenario);
        Assert.Equal("Cattle", Assert.Single(defaults.AffectedSpecies).Name);
    }

    [Fact]
    public async Task GetNewProfileDefaultsAsync_ReturnsNull_WhenTheVersionDoesNotExist()
    {
        var service = CreateService(new CapturingHandler(HttpStatusCode.NotFound, "{}"));

        Assert.Null(await service.GetNewProfileDefaultsAsync(CloneVersionId, isWhatIfScenario: false));
    }

    [Fact]
    public async Task GetNewProfileDefaultsAsync_Throws_OnAServerError()
    {
        var service = CreateService(new CapturingHandler(HttpStatusCode.InternalServerError, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetNewProfileDefaultsAsync(CloneVersionId, isWhatIfScenario: false));
    }

    [Fact]
    public async Task CreateProfileAsync_PostsTheRequest_AndReturnsTheNewProfileId()
    {
        var handler = new CapturingHandler(HttpStatusCode.Created, $$"""{ "newProfileId": "{{ProfileId}}", "newLastUpdated": "AQIDBAUGBwg=" }""");

        var result = await CreateService(handler).CreateProfileAsync(CreateRequest());

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/profiles", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("Anthrax", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(CloneVersionId, body.RootElement.GetProperty("cloneProfileVersionId").GetGuid());
        Assert.Equal(1, body.RootElement.GetProperty("affectedSpeciesInsertList").GetArrayLength());
        Assert.Equal(SaveProfileOutcome.Success, result.Outcome);
        Assert.Equal(ProfileId, result.NewProfileId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateProfileAsync_FallsBackToTheRequestedId_WhenTheResponseHasNone()
    {
        var request = CreateRequest();

        var result = await CreateService(new CapturingHandler(HttpStatusCode.Created, "null")).CreateProfileAsync(request);

        Assert.Equal(request.Id, result.NewProfileId);
    }

    [Fact]
    public async Task CreateProfileAsync_ReportsAValidationFailure_WithTheApisMessages()
    {
        const string json = """{ "errors": { "Title": ["A title is required."], "ParentTitle": ["A parent title is required."] } }""";

        var result = await CreateService(new CapturingHandler(HttpStatusCode.BadRequest, json)).CreateProfileAsync(CreateRequest());

        Assert.Equal(SaveProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("A title is required. A parent title is required.", result.ErrorMessage);
        Assert.Null(result.NewProfileId);
    }

    [Fact]
    public async Task CreateProfileAsync_ReportsAValidationFailure_WhenTheApiGivesNoMessages()
    {
        var result = await CreateService(new CapturingHandler(HttpStatusCode.BadRequest, "{}")).CreateProfileAsync(CreateRequest());

        Assert.Equal(SaveProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("The profile could not be created. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateProfileAsync_ReportsAnError_OnAServerError()
    {
        var result = await CreateService(new CapturingHandler(HttpStatusCode.InternalServerError, "{}")).CreateProfileAsync(CreateRequest());

        Assert.Equal(SaveProfileOutcome.Error, result.Outcome);
        Assert.Equal("The profile could not be created. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_PutsTheRequest()
    {
        var handler = new CapturingHandler(HttpStatusCode.NoContent, string.Empty);

        var result = await CreateService(handler).UpdateProfileAttributesAsync(ProfileId, UpdateRequest());

        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal($"/api/profiles/{ProfileId}", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(ProfileId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("AQIDBAUGBwg=", body.RootElement.GetProperty("lastUpdated").GetString());
        Assert.Equal(SaveProfileOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_ReportsAConflict_WithTheApisMessage()
    {
        const string json = """{ "detail": "Another user changed this profile." }""";

        var result = await CreateService(new CapturingHandler(HttpStatusCode.Conflict, json)).UpdateProfileAttributesAsync(ProfileId, UpdateRequest());

        Assert.Equal(SaveProfileOutcome.Conflict, result.Outcome);
        Assert.Equal("Another user changed this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_ReportsAConflict_WithADefaultMessage_WhenTheApiGivesNone()
    {
        var result = await CreateService(new CapturingHandler(HttpStatusCode.Conflict, "{}")).UpdateProfileAttributesAsync(ProfileId, UpdateRequest());

        Assert.Equal(SaveProfileOutcome.Conflict, result.Outcome);
        Assert.Equal("This profile has been edited by another user. Reload the page and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_ReportsAValidationFailure_WithTheReasonTheApiGives()
    {
        const string json = """{ "title": "Validation failed", "status": 400, "detail": "The profile title is already in use for a different profile" }""";

        var result = await CreateService(new CapturingHandler(HttpStatusCode.BadRequest, json)).UpdateProfileAttributesAsync(ProfileId, UpdateRequest());

        Assert.Equal(SaveProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("The profile title is already in use for a different profile", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_ReportsAnError_OnAServerError()
    {
        var result = await CreateService(new CapturingHandler(HttpStatusCode.InternalServerError, "{}")).UpdateProfileAttributesAsync(ProfileId, UpdateRequest());

        Assert.Equal(SaveProfileOutcome.Error, result.Outcome);
        Assert.Equal("The profile could not be saved. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public void Requests_AreSentWithTheSameJsonShapeTheApiBindsTo()
    {
        var json = JsonSerializer.Serialize(UpdateRequest(), JsonSerializerOptions.Web);

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
        Assert.Equal(["id", "title", "scenarioTitle", "lastUpdated", "affectedSpeciesDeleteList", "affectedSpeciesInsertList"], names);
    }
}
