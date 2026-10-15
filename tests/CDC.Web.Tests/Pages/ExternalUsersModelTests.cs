using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.UserAdmin;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class ExternalUsersModelTests
{
    private static readonly Guid ContributorRoleId = Guid.NewGuid();
    private static readonly Guid ReviewerRoleId = Guid.NewGuid();
    private static readonly Guid GrantedProfileId = Guid.NewGuid();
    private static readonly Guid AvailableProfileId = Guid.NewGuid();

    private sealed class FakeExternalUsersApiService : IExternalUsersApiService
    {
        public IReadOnlyList<MaintainedExternalUserDto> Users { get; init; } = [];

        public bool SaveSucceeds { get; init; } = true;

        public ExternalUserProfileAccessContract? Access { get; init; }

        public ExternalUserAccessResult AccessResult { get; init; } = new(true, null);

        public (Guid Id, bool Subscribed)? Saved { get; private set; }

        public SaveExternalUserProfileAccessContract? SavedAccess { get; private set; }

        public Guid? DeletedProfileId { get; private set; }

        public Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<MaintainedExternalUserDto>
            {
                Items = Users,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = Users.Count
            });

        public Task<bool> SetReviewEmailSubscriptionAsync(Guid id, bool subscribed, CancellationToken cancellationToken = default)
        {
            Saved = (id, subscribed);
            return Task.FromResult(SaveSucceeds);
        }

        public Task<ExternalUserProfileAccessContract?> GetProfileAccessAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Access);

        public Task<ExternalUserAccessResult> SaveProfileAccessAsync(
            Guid id, Guid profileId, SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken = default)
        {
            SavedAccess = request;
            return Task.FromResult(AccessResult);
        }

        public Task<bool> DeleteProfileAccessAsync(Guid id, Guid profileId, CancellationToken cancellationToken = default)
        {
            DeletedProfileId = profileId;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static readonly ProfileSectionMetadataDto[] Sections =
        [.. Enumerable.Range(1, 16).Select(number => new ProfileSectionMetadataDto { Id = Guid.NewGuid(), ShortName = $"Section {number}", SectionNumber = number })];

    private static ExternalUsersModel CreatePageModel(FakeExternalUsersApiService api)
    {
        var provider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext();

        var roles = new ProfileUserRoleDto[]
        {
            new() { Id = ContributorRoleId, Name = "Profile author", IsContributor = true },
            new() { Id = ReviewerRoleId, Name = "Profile reviewer", IsContributor = false }
        };

        return new ExternalUsersModel(
            api,
            new FakeProfileContributorsApiService(profileUserRoles: roles),
            new FakeProfileSectionsApiService(metadata: new ProfileQuestionnaireMetadataDto { Sections = Sections }),
            NullLogger<ExternalUsersModel>.Instance)
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

    private static MaintainedExternalUserDto User(bool subscribed = false) => new()
    {
        Id = Guid.NewGuid(),
        UserName = "jsmith",
        FullName = "Jo Smith",
        Organisation = "Org",
        SubscribedToReviewEmails = subscribed
    };

    private static ExternalUserProfileAccessContract AccessWithOneGrant() => new()
    {
        Profiles =
        [
            new ExternalUserProfileGrantContract
            {
                ProfileId = GrantedProfileId,
                Title = "Anthrax",
                RoleId = ContributorRoleId,
                RoleName = "Profile author",
                IsContributor = true,
                SectionPermissionIds = [Sections[0].Id, Sections[1].Id]
            }
        ],
        AvailableProfiles = [new ProfileOptionContract { Id = AvailableProfileId, Title = "Brucellosis" }]
    };

    [Fact]
    public async Task OnGet_OpensEditPanelWithCurrentSubscription()
    {
        var user = User(subscribed: true);
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.EditUserId = user.Id;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(user, model.EditUser);
        Assert.True(model.SubscribedToReviewEmails);
        Assert.Single(model.Access!.Profiles);
        Assert.False(model.IsAccessPanelOpen);
    }

    [Fact]
    public async Task OnGet_OpensAddPanelOfferingEveryRoleAndAllSixteenSections()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.EditUserId = user.Id;
        model.AddProfileId = AvailableProfileId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.NotNull(model.AddingProfile);
        Assert.Equal(2, model.ProfileUserRoles.Count);
        Assert.Equal(16, model.PermissionSections.Count);
        Assert.Empty(model.SelectedSectionIds);
        Assert.Equal(AvailableProfileId, model.ProfileId);
    }

    [Fact]
    public async Task OnGet_EditingGrantLoadsSavedRoleAndSections()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.EditUserId = user.Id;
        model.AccessProfileId = GrantedProfileId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.NotNull(model.EditingGrant);
        Assert.Equal(ContributorRoleId, model.RoleId);
        Assert.Equal([Sections[0].Id, Sections[1].Id], model.SelectedSectionIds);
    }

    [Fact]
    public async Task OnGet_AfterCancel_KeepsPreviouslySavedPermissions()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var model = CreatePageModel(api);
        model.EditUserId = user.Id;

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsAccessPanelOpen);
        Assert.Equal([Sections[0].Id, Sections[1].Id], model.Access!.Profiles[0].SectionPermissionIds);
        Assert.Null(api.SavedAccess);
    }

    [Fact]
    public async Task OnGet_IgnoresProfileNotAvailableToUser()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.EditUserId = user.Id;
        model.AddProfileId = Guid.NewGuid();

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsAccessPanelOpen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnPostSave_SendsSubscriptionAndRedirects(bool subscribe)
    {
        var user = User(subscribed: !subscribe);
        var api = new FakeExternalUsersApiService { Users = [user] };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.SubscribedToReviewEmails = subscribe;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal((user.Id, subscribe), api.Saved);
        Assert.NotNull(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostSave_ShowsError_WhenSaveFails()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], SaveSucceeds = false });
        model.UserId = user.Id;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.SaveError);
        Assert.Equal(user, model.EditUser);
    }

    [Fact]
    public async Task OnPostSaveAccess_SendsRoleAndSectionsAndRedirects()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = ContributorRoleId;
        model.SelectedSectionIds = [Sections[2].Id, Sections[3].Id, Guid.NewGuid()];

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(ContributorRoleId, api.SavedAccess!.RoleId);
        Assert.Equal([Sections[2].Id, Sections[3].Id], api.SavedAccess.SectionPermissionIds);
        Assert.NotNull(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostSaveAccess_DropsSectionsForReviewerRole()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = ReviewerRoleId;
        model.SelectedSectionIds = [Sections[0].Id];

        await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.Empty(api.SavedAccess!.SectionPermissionIds);
    }

    [Fact]
    public async Task OnPostSaveAccess_ReopensPanelKeepingEntries_WhenSaveFails()
    {
        var user = User();
        var api = new FakeExternalUsersApiService
        {
            Users = [user],
            Access = AccessWithOneGrant(),
            AccessResult = new ExternalUserAccessResult(false, "A contributor must have at least one profile section permission granted.")
        };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.ProfileId = GrantedProfileId;
        model.RoleId = ContributorRoleId;
        model.SelectedSectionIds = [];

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.AccessError);
        Assert.NotNull(model.EditingGrant);
        Assert.Empty(model.SelectedSectionIds);
    }

    [Fact]
    public async Task OnPostDeleteAccess_RemovesAccessAndRedirects()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.ProfileId = GrantedProfileId;

        var result = await model.OnPostDeleteAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(GrantedProfileId, api.DeletedProfileId);
        Assert.NotNull(model.SuccessMessage);
    }
}
