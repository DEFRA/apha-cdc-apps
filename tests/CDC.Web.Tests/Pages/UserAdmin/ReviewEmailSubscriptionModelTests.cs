using CDC.Web.Models;
using CDC.Web.Pages.UserAdmin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.UserAdmin;

public class ReviewEmailSubscriptionModelTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnGetAsync_PreselectsTheStoredSubscriptionState(bool subscribed)
    {
        var pageModel = CreatePageModel(new FakeUserAdminApiService(user: User(subscribed)));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(subscribed, pageModel.SubscribedToReviewEmails);
        Assert.Equal(Convert.ToBase64String(RowVersion), pageModel.LastUpdated);
        Assert.Equal("Joe Bloggs", pageModel.FullName);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoSuchUserExists()
    {
        var pageModel = CreatePageModel(new FakeUserAdminApiService(user: null));

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_ForAnUnknownUserType()
    {
        var pageModel = CreatePageModel(new FakeUserAdminApiService(user: User(true)), userType: "unknown");

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("global", "/UserAdmin/GlobalUsers")]
    [InlineData("external", "/UserAdmin/ExternalUsers")]
    public async Task OnPostAsync_SubscribesTheUserAndReturnsToTheCorrectList(string userType, string expectedPage)
    {
        var apiService = new FakeUserAdminApiService(user: User(false));
        var pageModel = CreatePageModel(apiService, userType);
        pageModel.SubscribedToReviewEmails = true;
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(expectedPage, redirect.PageName);
        Assert.Equal(UserId, redirect.RouteValues!["updatedUserId"]);
        Assert.True(apiService.LastUpdateRequest!.SubscribedToReviewEmails);
        Assert.Equal(RowVersion, apiService.LastUpdateRequest.LastUpdated);
    }

    [Fact]
    public async Task OnPostAsync_UnsubscribesTheUser()
    {
        var apiService = new FakeUserAdminApiService(user: User(true));
        var pageModel = CreatePageModel(apiService);
        pageModel.SubscribedToReviewEmails = false;
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(CancellationToken.None));
        Assert.False(apiService.LastUpdateRequest!.SubscribedToReviewEmails);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsAnErrorWhenNoChoiceIsMade()
    {
        var apiService = new FakeUserAdminApiService(user: User(true));
        var pageModel = CreatePageModel(apiService);
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        Assert.IsType<PageResult>(await pageModel.OnPostAsync(CancellationToken.None));
        Assert.Null(apiService.LastUpdateRequest);
        Assert.Contains(
            pageModel.ModelState[nameof(pageModel.SubscribedToReviewEmails)]!.Errors,
            error => error.ErrorMessage == "Select whether this user should receive review emails");
    }

    [Fact]
    public async Task OnPostAsync_ReturnsAnErrorWhenAnotherAdministratorSavedFirst()
    {
        var apiService = new FakeUserAdminApiService(
            user: User(true),
            updateResult: new UpdateReviewEmailSubscriptionResult(ReviewEmailSubscriptionOutcome.Conflict, "Reload and try again."));
        var pageModel = CreatePageModel(apiService);
        pageModel.SubscribedToReviewEmails = false;
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        Assert.IsType<PageResult>(await pageModel.OnPostAsync(CancellationToken.None));
        Assert.Contains(pageModel.ModelState[string.Empty]!.Errors, error => error.ErrorMessage == "Reload and try again.");
    }

    private static ReviewEmailSubscriptionModel CreatePageModel(
        FakeUserAdminApiService apiService,
        string userType = "global")
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new ReviewEmailSubscriptionModel(apiService, NullLogger<ReviewEmailSubscriptionModel>.Instance)
        {
            UserId = UserId,
            UserType = userType,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };
    }

    private static MaintainedUserDto User(bool subscribed) => new()
    {
        Id = UserId,
        UserName = "jbloggs",
        FullName = "Joe Bloggs",
        Organisation = "APHA",
        EmailAddress = "joe.bloggs@example.gov.uk",
        SubscribedToReviewEmails = subscribed,
        LastUpdated = RowVersion
    };
}
