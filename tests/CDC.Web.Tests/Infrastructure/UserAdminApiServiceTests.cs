using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class UserAdminApiServiceTests
{
    private const string UserListJson = """
        [
          {
            "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
            "userName": "jbloggs",
            "fullName": "Joe Bloggs",
            "organisation": "APHA",
            "emailAddress": "joe.bloggs@example.gov.uk",
            "subscribedToReviewEmails": true,
            "isExternal": false,
            "lastUpdated": "AAAAAAAAAAE="
          }
        ]
        """;

    private const string UserJson = """
        {
          "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
          "userName": "jbloggs",
          "fullName": "Joe Bloggs",
          "organisation": "APHA",
          "emailAddress": "joe.bloggs@example.gov.uk",
          "subscribedToReviewEmails": false,
          "isExternal": true,
          "lastUpdated": "AAAAAAAAAAE="
        }
        """;

    [Fact]
    public async Task GetGlobalUsersAsync_DeserialisesTheResponseBody()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, UserListJson));

        var users = await service.GetGlobalUsersAsync();

        var user = Assert.Single(users);
        Assert.Equal("Joe Bloggs", user.FullName);
        Assert.True(user.SubscribedToReviewEmails);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        Assert.Empty(await service.GetGlobalUsersAsync());
    }

    [Fact]
    public async Task GetExternalUsersAsync_DeserialisesTheResponseBody()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, UserListJson));

        Assert.Single(await service.GetExternalUsersAsync());
    }

    [Fact]
    public async Task GetExternalUsersAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        Assert.Empty(await service.GetExternalUsersAsync());
    }

    [Fact]
    public async Task GetUserAsync_DeserialisesTheResponseBody()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, UserJson));

        var user = await service.GetUserAsync(Guid.NewGuid());

        Assert.NotNull(user);
        Assert.False(user!.SubscribedToReviewEmails);
        Assert.True(user.IsExternal);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        Assert.Null(await service.GetUserAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetUserAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetUserAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, ReviewEmailSubscriptionOutcome.Success)]
    [InlineData(HttpStatusCode.Conflict, ReviewEmailSubscriptionOutcome.Conflict)]
    [InlineData(HttpStatusCode.NotFound, ReviewEmailSubscriptionOutcome.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, ReviewEmailSubscriptionOutcome.ValidationFailed)]
    [InlineData(HttpStatusCode.InternalServerError, ReviewEmailSubscriptionOutcome.Error)]
    public async Task UpdateReviewEmailSubscriptionAsync_MapsTheStatusCodeToAnOutcome(
        HttpStatusCode statusCode,
        ReviewEmailSubscriptionOutcome expected)
    {
        var service = CreateService(new FakeHttpMessageHandler(statusCode, string.Empty));

        var result = await service.UpdateReviewEmailSubscriptionAsync(new UpdateReviewEmailSubscriptionRequest
        {
            UserId = Guid.NewGuid(),
            SubscribedToReviewEmails = true,
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        });

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == ReviewEmailSubscriptionOutcome.Success, result.ErrorMessage is null);
    }

    private static UserAdminApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new UserAdminApiService(httpClient);
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
