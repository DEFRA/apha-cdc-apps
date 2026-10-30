using System.Security.Claims;
using CDC.Common.Contracts;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.UserAdmin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class GlobalUsersModelTests
{
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private sealed class FakeGlobalUsersApiService : IGlobalUsersApiService
    {
        public IReadOnlyList<GlobalUserDto> Users { get; init; } = [];

        public GlobalUserLookupResult Lookup { get; init; } = new() { Outcome = GlobalUserLookupOutcome.InvalidFormat };

        public GlobalUserActionResult ActionResult { get; init; } = new() { Outcome = GlobalUserActionOutcome.Success };

        public bool Throw { get; init; }

        public bool ThrowOnActions { get; init; }

        public SaveGlobalUserRequest? AddedRequest { get; private set; }

        public (Guid Id, SaveGlobalUserRequest Request)? Updated { get; private set; }

        public Guid? DeletedId { get; private set; }

        public (GlobalUserSortColumn Column, bool Descending)? RequestedSort { get; private set; }

        public Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(
            int pageNumber, int pageSize, GlobalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default)
        {
            RequestedSort = (sortColumn, sortDescending);

            return Throw
                ? Task.FromException<PagedResult<GlobalUserDto>>(new HttpRequestException("boom"))
                : Task.FromResult(new PagedResult<GlobalUserDto>
                {
                    Items = Users,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalRecords = Users.Count
                });
        }

        public Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default) =>
            ThrowOnActions ? Task.FromException<GlobalUserLookupResult>(new HttpRequestException("boom")) : Task.FromResult(Lookup);

        public Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default)
        {
            AddedRequest = request;
            return ThrowOnActions ? Task.FromException<GlobalUserActionResult>(new HttpRequestException("boom")) : Task.FromResult(ActionResult);
        }

        public Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default)
        {
            Updated = (id, request);
            return ThrowOnActions ? Task.FromException<GlobalUserActionResult>(new HttpRequestException("boom")) : Task.FromResult(ActionResult);
        }

        public Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default)
        {
            DeletedId = id;
            return ThrowOnActions ? Task.FromException<GlobalUserActionResult>(new HttpRequestException("boom")) : Task.FromResult(ActionResult);
        }
    }

    private static GlobalUsersModel CreatePageModel(FakeGlobalUsersApiService api, bool signedInAsCurrentUser = true)
    {
        var provider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext();

        if (signedInAsCurrentUser)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ExternalUserClaimTypes.InternalUserId, CurrentUserId.ToString())], "test"));
        }

        return new GlobalUsersModel(api, NullLogger<GlobalUsersModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(provider, new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            MetadataProvider = provider
        };
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static GlobalUserDto User(Guid? id = null, GlobalUserRole role = GlobalUserRole.ProfileEditor) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserName = @"DEFRA\jsmith",
        FullName = "Jo Smith",
        Organisation = "APHA",
        Role = role,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    [Fact]
    public async Task OnGet_LoadsUsers()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { Users = [User(), User()] });

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, model.Users.Count);
        Assert.False(model.HasLoadError);
    }

    [Fact]
    public async Task OnGet_SortsByFullNameAscending_ByDefault()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal((GlobalUserSortColumn.FullName, false), api.RequestedSort);
        Assert.Equal("ascending", model.GetAriaSort(GlobalUserSortColumn.FullName));
        Assert.Equal("none", model.GetAriaSort(GlobalUserSortColumn.UserName));
    }

    [Fact]
    public async Task OnGet_PassesTheRequestedSortToTheApi()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.SortColumn = GlobalUserSortColumn.Organisation;
        model.SortDirection = "Descending";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal((GlobalUserSortColumn.Organisation, true), api.RequestedSort);
        Assert.Equal("descending", model.GetAriaSort(GlobalUserSortColumn.Organisation));
    }

    [Fact]
    public async Task OnPostLookupUser_ShowsEditHeading_ForAnExistingUser()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService
        {
            Lookup = new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.ExistingUser, Id = Guid.NewGuid(), UserName = @"DEFRA\jsmith" }
        });
        model.UserName = @"DEFRA\jsmith";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.True(model.IsNewUser);
        Assert.True(model.IsExistingUser);
        Assert.Equal("Edit profile editor", model.PanelHeading);
    }

    [Fact]
    public async Task OnPostLookupUser_ShowsAddHeading_ForANewUser()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService
        {
            Lookup = new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.NewUser, Id = Guid.NewGuid(), UserName = @"DEFRA\newuser" }
        });
        model.UserName = @"DEFRA\newuser";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.False(model.IsExistingUser);
        Assert.Equal("Add global user", model.PanelHeading);
    }

    [Fact]
    public async Task OnGet_SetsLoadError_WhenApiFails()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { Throw = true });

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.HasLoadError);
        Assert.Empty(model.Users);
    }

    [Fact]
    public async Task OnGet_OpensEditForm_WithUserDetails()
    {
        var user = User(role: GlobalUserRole.PolicyProfileUser);
        var model = CreatePageModel(new FakeGlobalUsersApiService { Users = [user] });
        model.EditUserId = user.Id;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.ShowForm);
        Assert.False(model.IsNewUser);
        Assert.Equal(user.FullName, model.FullName);
        Assert.Equal(GlobalUserRole.PolicyProfileUser, model.Role);
    }

    [Fact]
    public async Task OnGet_OpensLookup_WhenAdding()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService());
        model.AddUser = true;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.ShowLookup);
        Assert.False(model.ShowForm);
    }

    [Fact]
    public async Task Lookup_RequiresUsername()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService());
        model.UserName = "  ";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.True(model.ShowLookup);
        Assert.True(model.FieldErrors.ContainsKey("UserName"));
    }

    [Theory]
    [InlineData(GlobalUserLookupOutcome.Blocked)]
    [InlineData(GlobalUserLookupOutcome.SsoUser)]
    [InlineData(GlobalUserLookupOutcome.InvalidFormat)]
    public async Task Lookup_ShowsError_ForRejectedOutcomes(GlobalUserLookupOutcome outcome)
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { Lookup = new GlobalUserLookupResult { Outcome = outcome } });
        model.UserName = @"DEFRA\x";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.True(model.ShowLookup);
        Assert.False(model.ShowForm);
        Assert.True(model.FieldErrors.ContainsKey("UserName"));
    }

    [Fact]
    public async Task Lookup_OpensForm_ForExistingUser()
    {
        var id = Guid.NewGuid();
        var model = CreatePageModel(new FakeGlobalUsersApiService
        {
            Lookup = new GlobalUserLookupResult
            {
                Outcome = GlobalUserLookupOutcome.ExistingUser,
                Id = id,
                UserName = @"DEFRA\jsmith",
                FullName = "Jo Smith",
                Organisation = "APHA"
            }
        });
        model.UserName = @"DEFRA\jsmith";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.True(model.ShowForm);
        Assert.True(model.IsNewUser);
        Assert.Equal(id, model.UserId);
        Assert.Null(model.LookupWarning);
    }

    [Fact]
    public async Task Lookup_WarnsAndOpensForm_ForNewUser()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService
        {
            Lookup = new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.NewUser, Id = Guid.NewGuid(), UserName = @"DEFRA\new" }
        });
        model.UserName = @"DEFRA\new";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.True(model.ShowForm);
        Assert.NotNull(model.LookupWarning);
    }

    [Fact]
    public async Task Save_Add_CallsAddAndRedirects()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.IsNewUser = true;
        model.UserId = Guid.NewGuid();
        model.UserName = @"DEFRA\jsmith";
        model.FullName = " Jo Smith ";
        model.Organisation = "APHA";
        model.Role = GlobalUserRole.ProfileEditor;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Jo Smith", api.AddedRequest!.FullName);
        Assert.NotNull(model.SuccessMessage);
    }

    [Fact]
    public async Task Save_Edit_CallsUpdate()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.UserId = Guid.NewGuid();
        model.FullName = "Jo Smith";
        model.Organisation = "APHA";
        model.Role = GlobalUserRole.PolicyProfileUser;

        await model.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal(model.UserId, api.Updated!.Value.Id);
        Assert.Equal(GlobalUserRole.PolicyProfileUser, api.Updated.Value.Request.Role);
    }

    [Fact]
    public async Task Save_ShowsFieldErrors_AndSkipsApi_WhenInvalid()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.UserId = Guid.NewGuid();

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.FieldErrors.ContainsKey("Role"));
        Assert.True(model.FieldErrors.ContainsKey("FullName"));
        Assert.True(model.FieldErrors.ContainsKey("Organisation"));
        Assert.Null(api.Updated);
    }

    [Fact]
    public async Task Save_BlocksRemovingOwnProfileEditorRights()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.UserId = CurrentUserId;
        model.FullName = "Me";
        model.Organisation = "APHA";
        model.Role = GlobalUserRole.PolicyProfileUser;

        await model.OnPostSaveAsync(CancellationToken.None);

        Assert.True(model.FieldErrors.ContainsKey("Role"));
        Assert.Null(api.Updated);
    }

    [Fact]
    public async Task Save_ShowsApiError_WhenApiRejects()
    {
        var api = new FakeGlobalUsersApiService
        {
            ActionResult = new GlobalUserActionResult { Outcome = GlobalUserActionOutcome.Conflict, ErrorMessage = "Changed." }
        };
        var model = CreatePageModel(api);
        model.UserId = Guid.NewGuid();
        model.FullName = "Jo";
        model.Organisation = "APHA";
        model.Role = GlobalUserRole.ProfileEditor;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Changed.", model.GeneralErrors);
        Assert.True(model.ShowForm);
    }

    [Fact]
    public async Task Delete_CallsApiAndReportsSuccess()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.UserId = Guid.NewGuid();

        await model.OnPostDeleteAsync(CancellationToken.None);

        Assert.Equal(model.UserId, api.DeletedId);
        Assert.NotNull(model.SuccessMessage);
    }

    [Fact]
    public async Task Delete_RefusesToDeleteSelf()
    {
        var api = new FakeGlobalUsersApiService();
        var model = CreatePageModel(api);
        model.UserId = CurrentUserId;

        await model.OnPostDeleteAsync(CancellationToken.None);

        Assert.Null(api.DeletedId);
        Assert.NotNull(model.DeleteErrorMessage);
    }

    [Fact]
    public async Task Delete_ReportsApiFailure()
    {
        var api = new FakeGlobalUsersApiService
        {
            ActionResult = new GlobalUserActionResult { Outcome = GlobalUserActionOutcome.Conflict, ErrorMessage = "Changed." }
        };
        var model = CreatePageModel(api);
        model.UserId = Guid.NewGuid();

        await model.OnPostDeleteAsync(CancellationToken.None);

        Assert.Equal("Changed.", model.DeleteErrorMessage);
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task Delete_ReportsError_WhenApiThrows()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { ThrowOnActions = true });
        model.UserId = Guid.NewGuid();

        await model.OnPostDeleteAsync(CancellationToken.None);

        Assert.Equal("We could not delete this global user. Try again later.", model.DeleteErrorMessage);
    }

    [Fact]
    public async Task Lookup_ShowsGeneralError_WhenApiThrows()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { ThrowOnActions = true });
        model.UserName = @"DEFRA\x";

        await model.OnPostLookupUserAsync(CancellationToken.None);

        Assert.Single(model.GeneralErrors);
        Assert.False(model.ShowForm);
    }

    [Fact]
    public async Task Save_ShowsGeneralError_WhenApiThrows()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { ThrowOnActions = true });
        model.UserId = Guid.NewGuid();
        model.FullName = "Jo";
        model.Organisation = "APHA";
        model.Role = GlobalUserRole.ProfileEditor;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("We could not save your changes. Try again later.", model.GeneralErrors);
    }

    [Fact]
    public async Task Save_RejectsOverlongNames()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService());
        model.UserId = Guid.NewGuid();
        model.FullName = new string('a', 101);
        model.Organisation = new string('b', 101);
        model.Role = GlobalUserRole.ProfileEditor;

        await model.OnPostSaveAsync(CancellationToken.None);

        Assert.True(model.FieldErrors.ContainsKey("FullName"));
        Assert.True(model.FieldErrors.ContainsKey("Organisation"));
    }

    [Theory]
    [InlineData("All")]
    [InlineData("20")]
    [InlineData("junk")]
    public async Task OnGet_AcceptsAnyPageSizeSelection(string pageSize)
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService { Users = [User()] });
        model.PageSize = pageSize;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Single(model.Users);
    }

    [Fact]
    public void IsCurrentUser_IsFalse_WhenNotSignedIn()
    {
        var model = CreatePageModel(new FakeGlobalUsersApiService(), signedInAsCurrentUser: false);

        Assert.False(model.IsCurrentUser(CurrentUserId));
    }
}
