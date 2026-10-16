using System.Net;
using System.Text;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class ExternalUsersApiServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetExternalUsersAsync_RequestsThePageAndDeserialisesTheBody()
    {
        const string json = """{"items":[{"fullName":"Jo Smith"}],"pageNumber":2,"pageSize":10,"totalRecords":11}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);

        var result = await CreateClient(handler).GetExternalUsersAsync(2, 10);

        Assert.Equal("/api/external-users?pageNumber=2&pageSize=10", handler.LastRequestUri!.PathAndQuery);
        Assert.Equal("Jo Smith", Assert.Single(result.Items).FullName);
    }

    [Fact]
    public async Task GetExternalUsersAsync_ReturnsEmptyPage_WhenTheBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await client.GetExternalUsersAsync(3, 15);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.PageNumber);
    }

    [Fact]
    public async Task SetReviewEmailSubscriptionAsync_PutsTheSubscription()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        var saved = await CreateClient(handler).SetReviewEmailSubscriptionAsync(UserId, true);

        Assert.True(saved);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/api/external-users/{UserId}/review-email-subscription", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("\"subscribedToReviewEmails\":true", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetReviewEmailSubscriptionAsync_ReturnsFalse_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        Assert.False(await client.SetReviewEmailSubscriptionAsync(UserId, false));
    }

    [Fact]
    public async Task GetProfileAccessAsync_DeserialisesTheBody()
    {
        const string json = """{"profiles":[{"title":"Anthrax"}],"availableProfiles":[]}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);

        var result = await CreateClient(handler).GetProfileAccessAsync(UserId);

        Assert.Equal($"/api/external-users/{UserId}/profile-access", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal("Anthrax", Assert.Single(result!.Profiles).Title);
    }

    [Fact]
    public async Task GetProfileAccessAsync_ReturnsNull_WhenNotFound()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        Assert.Null(await client.GetProfileAccessAsync(UserId));
    }

    [Fact]
    public async Task GetProfileAccessAsync_Throws_OnOtherFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProfileAccessAsync(UserId));
    }

    [Fact]
    public async Task SaveProfileAccessAsync_PutsTheRequest_AndReportsSuccess()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        var result = await CreateClient(handler).SaveProfileAccessAsync(UserId, ProfileId, new SaveExternalUserProfileAccessContract());

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/api/external-users/{UserId}/profile-access/{ProfileId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_ReportsConflict()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await client.SaveProfileAccessAsync(UserId, ProfileId, new SaveExternalUserProfileAccessContract());

        Assert.False(result.Succeeded);
        Assert.Contains("Another user has changed this access", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_ReturnsProblemDetail_OnBadRequest()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """{"detail":"Pick a section."}"""));

        var result = await client.SaveProfileAccessAsync(UserId, ProfileId, new SaveExternalUserProfileAccessContract());

        Assert.False(result.Succeeded);
        Assert.Equal("Pick a section.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("not json")]
    public async Task SaveProfileAccessAsync_ReturnsDefaultMessage_WhenBadRequestHasNoUsableDetail(string body)
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, body));

        var result = await client.SaveProfileAccessAsync(UserId, ProfileId, new SaveExternalUserProfileAccessContract());

        Assert.Equal("Please correct the highlighted fields.", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_ReturnsGenericError_OnAnyOtherFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.SaveProfileAccessAsync(UserId, ProfileId, new SaveExternalUserProfileAccessContract());

        Assert.Equal("We could not save this change. Try again later.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task DeleteProfileAccessAsync_ReflectsTheStatusCode(HttpStatusCode statusCode, bool expected)
    {
        var handler = new RecordingHttpMessageHandler(statusCode, string.Empty);

        var deleted = await CreateClient(handler).DeleteProfileAccessAsync(UserId, ProfileId);

        Assert.Equal(expected, deleted);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal($"/api/external-users/{UserId}/profile-access/{ProfileId}", handler.LastRequestUri!.AbsolutePath);
    }

    private static ExternalUsersApiService CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
