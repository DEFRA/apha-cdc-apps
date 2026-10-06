using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.UserAdmin;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the user administration pages against a fake CDC.Api, so the subscription control
// markup itself is exercised rather than just the page models.
public class UserAdminIntegrationTests
{
    private static readonly Guid UserId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");

    [Theory]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/UserAdmin/ExternalUsers")]
    public async Task UserList_ShowsEachUsersSubscriptionStateAndAChangeLink(string url)
    {
        var (statusCode, body) = await GetAsync(new FakeUserAdminApiService(users: [User(subscribed: true)]), url);

        Assert.Equal(System.Net.HttpStatusCode.OK, statusCode);
        Assert.Contains("Joe Bloggs", body);
        Assert.Contains("Subscribed", body);
        Assert.Contains($"/UserAdmin/ReviewEmailSubscription/{UserId}", body);
    }

    [Theory]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/UserAdmin/ExternalUsers")]
    public async Task UserList_ShowsAnEmptyMessage_WhenThereAreNoUsers(string url)
    {
        var (statusCode, body) = await GetAsync(new FakeUserAdminApiService(users: []), url);

        Assert.Equal(System.Net.HttpStatusCode.OK, statusCode);
        Assert.Contains("There are no", body);
    }

    [Theory]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/UserAdmin/ExternalUsers")]
    public async Task UserList_ShowsAnErrorSummary_WhenTheApiIsUnavailable(string url)
    {
        var (statusCode, body) = await GetAsync(
            new FakeUserAdminApiService(throwOnRead: new HttpRequestException("connection refused")), url);

        Assert.Equal(System.Net.HttpStatusCode.OK, statusCode);
        Assert.Contains("govuk-error-summary", body);
    }

    [Theory]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/UserAdmin/ExternalUsers")]
    public async Task UserList_ConfirmsASubscriptionChange(string url)
    {
        var (statusCode, body) = await GetAsync(
            new FakeUserAdminApiService(users: [User(subscribed: false)]), $"{url}?updatedUserId={UserId}");

        Assert.Equal(System.Net.HttpStatusCode.OK, statusCode);
        Assert.Contains("govuk-notification-banner--success", body);
        Assert.Contains("is now unsubscribed from review emails", body);
    }

    [Theory]
    [InlineData("global", "Global users")]
    [InlineData("external", "External users")]
    public async Task SubscriptionPage_OffersBothSubscribeAndUnsubscribeOptions(string userType, string listPageName)
    {
        var (statusCode, body) = await GetAsync(
            new FakeUserAdminApiService(user: User(subscribed: true)),
            $"/UserAdmin/ReviewEmailSubscription/{UserId}?userType={userType}");

        Assert.Equal(System.Net.HttpStatusCode.OK, statusCode);
        Assert.Contains("Yes, subscribe to review emails", body);
        Assert.Contains("No, unsubscribe from review emails", body);
        Assert.Contains(listPageName, body);
        Assert.Contains("govuk-radios__input", body);
    }

    [Fact]
    public async Task SubscriptionPage_PreselectsTheStoredState()
    {
        var (_, body) = await GetAsync(
            new FakeUserAdminApiService(user: User(subscribed: false)),
            $"/UserAdmin/ReviewEmailSubscription/{UserId}");

        var noRadioIndex = body.IndexOf("SubscribedToReviewEmails-no", StringComparison.Ordinal);
        Assert.True(noRadioIndex > 0);
        Assert.Contains("checked=\"checked\"", body[noRadioIndex..(noRadioIndex + 300)], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubscriptionPage_TellsTheAdministratorWhenNoEmailAddressIsRecorded()
    {
        var (_, body) = await GetAsync(
            new FakeUserAdminApiService(user: User(subscribed: true) with { EmailAddress = null }),
            $"/UserAdmin/ReviewEmailSubscription/{UserId}");

        Assert.Contains("No email address is recorded for this user", body);
    }

    [Fact]
    public async Task SubscriptionPage_ReturnsNotFound_WhenNoSuchUserExists()
    {
        var (statusCode, _) = await GetAsync(
            new FakeUserAdminApiService(user: null),
            $"/UserAdmin/ReviewEmailSubscription/{UserId}");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, statusCode);
    }

    private static async Task<(System.Net.HttpStatusCode StatusCode, string Body)> GetAsync(
        IUserAdminApiService apiService,
        string url)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAdminApiService>();
                services.AddSingleton(apiService);
            }));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);

        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static MaintainedUserDto User(bool subscribed) => new()
    {
        Id = UserId,
        UserName = "jbloggs",
        FullName = "Joe Bloggs",
        Organisation = "APHA",
        EmailAddress = "joe.bloggs@example.gov.uk",
        SubscribedToReviewEmails = subscribed,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };
}
