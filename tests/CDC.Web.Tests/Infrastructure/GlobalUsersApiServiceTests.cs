using System.Net;
using System.Text;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class GlobalUsersApiServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetGlobalUsersAsync_RequestsThePageAndDeserialisesTheBody()
    {
        const string json = """{"items":[{"fullName":"Jo Smith"}],"pageNumber":2,"pageSize":10,"totalRecords":11}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.GetGlobalUsersAsync(2, 10);

        Assert.Equal("/api/global-users?pageNumber=2&pageSize=10", handler.LastRequestUri!.PathAndQuery);
        Assert.Equal("Jo Smith", Assert.Single(result.Items).FullName);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_ReturnsEmptyPage_WhenTheBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await client.GetGlobalUsersAsync(3, 15);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.PageNumber);
    }

    [Fact]
    public async Task LookupAsync_EscapesTheUserNameAndDeserialisesTheBody()
    {
        const string json = """{"outcome":1}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.LookupAsync(@"DEFRA\jsmith");

        Assert.Equal("/api/global-users/lookup?userName=DEFRA%5Cjsmith", handler.LastRequestUri!.PathAndQuery);
        Assert.Equal(GlobalUserLookupOutcome.NewUser, result.Outcome);
    }

    [Fact]
    public async Task LookupAsync_ReturnsInvalidFormat_WhenTheBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await client.LookupAsync("x");

        Assert.Equal(GlobalUserLookupOutcome.InvalidFormat, result.Outcome);
    }

    [Fact]
    public async Task AddAsync_PostsToTheCollectionEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Created, string.Empty);
        var client = CreateClient(handler);

        var result = await client.AddAsync(new SaveGlobalUserRequest());

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/global-users", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(GlobalUserActionOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_PutsToTheUserEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(UserId, new SaveGlobalUserRequest());

        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/api/global-users/{UserId}", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(GlobalUserActionOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_SendsTheRowVersionAsBase64()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.DeleteAsync(UserId, [1, 2, 3]);

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Contains("lastUpdated=AQID", handler.LastRequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(GlobalUserActionOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task Save_ReturnsConflict_OnConflictStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await client.UpdateAsync(UserId, new SaveGlobalUserRequest());

        Assert.Equal(GlobalUserActionOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task Save_ReturnsProblemDetail_OnBadRequest()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """{"detail":"Name is too long."}"""));

        var result = await client.AddAsync(new SaveGlobalUserRequest());

        Assert.Equal(GlobalUserActionOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("Name is too long.", result.ErrorMessage);
    }

    [Fact]
    public async Task Save_ReturnsDefaultMessage_WhenBadRequestHasNoDetail()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, "{}"));

        var result = await client.AddAsync(new SaveGlobalUserRequest());

        Assert.Equal("Correct the errors and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task Save_ReturnsDefaultMessage_WhenBadRequestBodyIsNotJson()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, "not json"));

        var result = await client.AddAsync(new SaveGlobalUserRequest());

        Assert.Equal("Correct the errors and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task Delete_ReturnsGenericError_OnAnyOtherFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.DeleteAsync(UserId, []);

        Assert.Equal(GlobalUserActionOutcome.Error, result.Outcome);
        Assert.Equal("We could not delete this global user. Try again later.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(GlobalUserRole.ProfileEditor, "Profile editor")]
    [InlineData(GlobalUserRole.PolicyProfileUser, "Policy profile user")]
    [InlineData(GlobalUserRole.None, "")]
    public void ToDisplayName_DescribesTheRole(GlobalUserRole role, string expected)
    {
        Assert.Equal(expected, role.ToDisplayName());
    }

    private static GlobalUsersApiService CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new GlobalUsersApiService(httpClient);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
