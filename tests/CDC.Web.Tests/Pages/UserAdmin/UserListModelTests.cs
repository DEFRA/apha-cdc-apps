using CDC.Web.Models;
using CDC.Web.Pages.UserAdmin;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.UserAdmin;

public class UserListModelTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GlobalUsers_OnGetAsync_ListsUsersWithTheirSubscriptionState()
    {
        var pageModel = new GlobalUsersModel(
            new FakeUserAdminApiService(users: [User(subscribed: true)]),
            NullLogger<GlobalUsersModel>.Instance);

        await pageModel.OnGetAsync(null, CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.True(Assert.Single(pageModel.Users).SubscribedToReviewEmails);
        Assert.Null(pageModel.SuccessMessage);
    }

    [Fact]
    public async Task GlobalUsers_OnGetAsync_ConfirmsAnUnsubscribe()
    {
        var pageModel = new GlobalUsersModel(
            new FakeUserAdminApiService(users: [User(subscribed: false)]),
            NullLogger<GlobalUsersModel>.Instance);

        await pageModel.OnGetAsync(UserId, CancellationToken.None);

        Assert.Equal("Joe Bloggs is now unsubscribed from review emails.", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task ExternalUsers_OnGetAsync_ConfirmsASubscribe()
    {
        var pageModel = new ExternalUsersModel(
            new FakeUserAdminApiService(users: [User(subscribed: true)]),
            NullLogger<ExternalUsersModel>.Instance);

        await pageModel.OnGetAsync(UserId, CancellationToken.None);

        Assert.Equal("Joe Bloggs is now subscribed to review emails.", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task ExternalUsers_OnGetAsync_FlagsAnError_WhenTheListCannotBeLoaded()
    {
        var pageModel = new ExternalUsersModel(
            new FakeUserAdminApiService(throwOnRead: new HttpRequestException("connection refused")),
            NullLogger<ExternalUsersModel>.Instance);

        await pageModel.OnGetAsync(null, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.Empty(pageModel.Users);
    }

    private static MaintainedUserDto User(bool subscribed) => new()
    {
        Id = UserId,
        UserName = "jbloggs",
        FullName = "Joe Bloggs",
        Organisation = "APHA",
        SubscribedToReviewEmails = subscribed,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };
}
