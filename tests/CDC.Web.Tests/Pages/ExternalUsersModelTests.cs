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

        public bool DeleteSucceeds { get; init; } = true;

        public Exception? ThrowOnSetSubscription { get; init; }

        public Exception? ThrowOnGetAccess { get; init; }

        public Exception? ThrowOnSaveAccess { get; init; }

        public Exception? ThrowOnDelete { get; init; }

        public (Guid Id, bool Subscribed)? Saved { get; private set; }

        public SaveExternalUserProfileAccessContract? SavedAccess { get; private set; }

        public Guid? DeletedProfileId { get; private set; }

        public ExternalUserAccessResult PreRegisterResult { get; init; } = new(true, null);

        public Exception? ThrowOnPreRegister { get; init; }

        public PreRegisterExternalUserContract? PreRegistered { get; private set; }

        public Task<ExternalUserAccessResult> PreRegisterExternalUserAsync(PreRegisterExternalUserContract request, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPreRegister is not null)
            {
                throw ThrowOnPreRegister;
            }

            PreRegistered = request;
            return Task.FromResult(PreRegisterResult);
        }

        public (ExternalUserSortColumn Column, bool Descending)? RequestedSort { get; private set; }

        public Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(
            int pageNumber, int pageSize, ExternalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default)
        {
            RequestedSort = (sortColumn, sortDescending);

            return Task.FromResult(new PagedResult<MaintainedExternalUserDto>
            {
                Items = Users,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = Users.Count
            });
        }

        public Task<bool> SetReviewEmailSubscriptionAsync(Guid id, bool subscribed, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSetSubscription is not null)
            {
                throw ThrowOnSetSubscription;
            }

            Saved = (id, subscribed);
            return Task.FromResult(SaveSucceeds);
        }

        public Task<ExternalUserProfileAccessContract?> GetProfileAccessAsync(Guid id, CancellationToken cancellationToken = default) =>
            ThrowOnGetAccess is not null
                ? Task.FromException<ExternalUserProfileAccessContract?>(ThrowOnGetAccess)
                : Task.FromResult(Access);

        public Task<ExternalUserAccessResult> SaveProfileAccessAsync(
            Guid id, Guid profileId, SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSaveAccess is not null)
            {
                throw ThrowOnSaveAccess;
            }

            SavedAccess = request;
            return Task.FromResult(AccessResult);
        }

        public Task<bool> DeleteProfileAccessAsync(Guid id, Guid profileId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDelete is not null)
            {
                throw ThrowOnDelete;
            }

            DeletedProfileId = profileId;
            return Task.FromResult(DeleteSucceeds);
        }

        public ExternalUserAccessResult SendInstructionsResult { get; init; } = new(true, null);

        public Exception? ThrowOnSendInstructions { get; init; }

        public Guid? InstructionsSentTo { get; private set; }

        public Task<ExternalUserAccessResult> SendLoginInstructionsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSendInstructions is not null)
            {
                throw ThrowOnSendInstructions;
            }

            InstructionsSentTo = id;
            return Task.FromResult(SendInstructionsResult);
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

    private static ExternalUsersModel CreatePageModel(FakeExternalUsersApiService api, Exception? throwOnGetRoles = null, ISession? session = null)
    {
        var provider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext { Session = session ?? new FakeSession() };

        var roles = new ProfileUserRoleDto[]
        {
            new() { Id = ContributorRoleId, Name = "Profile author", IsContributor = true },
            new() { Id = ReviewerRoleId, Name = "Profile reviewer", IsContributor = false }
        };

        return new ExternalUsersModel(
            api,
            new FakeProfileContributorsApiService(profileUserRoles: roles, throwOnGetProfileUserRoles: throwOnGetRoles),
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
    public async Task OnGet_SortsByFullNameAscending_ByDefault()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal((ExternalUserSortColumn.FullName, false), api.RequestedSort);
        Assert.Equal("ascending", model.GetAriaSort(ExternalUserSortColumn.FullName));
        Assert.Equal("none", model.GetAriaSort(ExternalUserSortColumn.UserName));
    }

    [Fact]
    public async Task OnGet_PassesTheRequestedSortToTheApi()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);
        model.SortColumn = ExternalUserSortColumn.Organisation;
        model.SortDirection = "Descending";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal((ExternalUserSortColumn.Organisation, true), api.RequestedSort);
        Assert.Equal("descending", model.GetAriaSort(ExternalUserSortColumn.Organisation));
        Assert.Equal("none", model.GetAriaSort(ExternalUserSortColumn.UserName));
    }

    [Fact]
    public void BuildSortUrl_StartsAscending_ThenTogglesAndResetsToThePageOne()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());
        var url = new StubUrlHelper("/UserAdmin/ExternalUsers");
        model.Url = url;
        model.PageNumber = 3;
        model.SortColumn = ExternalUserSortColumn.FullName;
        model.SortDirection = "Ascending";

        model.BuildSortUrl(ExternalUserSortColumn.FullName);
        Assert.Equal("Descending", url.LastValues!["SortDirection"]);
        Assert.Equal(1, url.LastValues["PageNumber"]);

        model.BuildSortUrl(ExternalUserSortColumn.UserName);
        Assert.Equal(ExternalUserSortColumn.UserName, url.LastValues["SortColumn"]);
        Assert.Equal("Ascending", url.LastValues["SortDirection"]);
    }

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
    public async Task OnPostSaveAccess_StagesRoleAndSectionsWithoutSavingAndRedirects()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var model = CreatePageModel(api, session: session);
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = ContributorRoleId;
        model.SelectedSectionIds = [Sections[2].Id, Sections[3].Id, Guid.NewGuid()];

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(api.SavedAccess);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        Assert.True(next.HasPendingChanges);
        Assert.Equal(2, next.Access!.Profiles.Count);
        var staged = next.Access.Profiles.Single(profile => profile.ProfileId == AvailableProfileId);
        Assert.Equal("Brucellosis", staged.Title);
        Assert.Equal("Profile author", staged.RoleName);
        Assert.Equal([Sections[2].Id, Sections[3].Id], staged.SectionPermissionIds);
        Assert.Empty(next.Access.AvailableProfiles);
    }

    [Fact]
    public async Task OnPostSaveAccess_DropsSectionsForReviewerRole()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var model = CreatePageModel(api, session: session);
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = ReviewerRoleId;
        model.SelectedSectionIds = [Sections[0].Id];

        await model.OnPostSaveAccessAsync(CancellationToken.None);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        Assert.Empty(next.Access!.Profiles.Single(profile => profile.ProfileId == AvailableProfileId).SectionPermissionIds);
    }

    [Fact]
    public async Task OnPostSaveAccess_ReopensPanelKeepingEntries_WhenNoSectionIsTicked()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var model = CreatePageModel(api);
        model.UserId = user.Id;
        model.ProfileId = GrantedProfileId;
        model.RoleId = ContributorRoleId;
        model.SelectedSectionIds = [];

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("At least one section must be ticked.", model.AccessError);
        Assert.NotNull(model.EditingGrant);
        Assert.Empty(model.SelectedSectionIds);
        Assert.False(model.HasPendingChanges);
    }

    [Fact]
    public async Task OnPostSaveAccess_ShowsError_WhenRoleIsNotSelected()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = Guid.Empty;

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Please select a valid role.", model.AccessError);
        Assert.NotNull(model.AddingProfile);
    }

    [Fact]
    public async Task OnPostDeleteAccess_StagesRemovalWithoutDeletingAndRedirects()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var model = CreatePageModel(api, session: session);
        model.UserId = user.Id;
        model.ProfileId = GrantedProfileId;

        var result = await model.OnPostDeleteAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(api.DeletedProfileId);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        Assert.True(next.HasPendingChanges);
        Assert.Empty(next.Access!.Profiles);
        Assert.Contains(next.Access.AvailableProfiles, profile => profile.Id == GrantedProfileId);
    }

    [Fact]
    public async Task OnPostDeleteAccess_DiscardsAStagedAdd_WithoutQueueingADelete()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var stage = CreatePageModel(api, session: session);
        stage.UserId = user.Id;
        stage.ProfileId = AvailableProfileId;
        stage.RoleId = ReviewerRoleId;
        await stage.OnPostSaveAccessAsync(CancellationToken.None);

        var delete = CreatePageModel(api, session: session);
        delete.UserId = user.Id;
        delete.ProfileId = AvailableProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        Assert.False(next.HasPendingChanges);
        Assert.Single(next.Access!.Profiles);
    }

    [Fact]
    public async Task OnPostSaveAccess_ReplacesAStagedRemoval_WhenTheProfileIsAddedAgain()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var delete = CreatePageModel(api, session: session);
        delete.UserId = user.Id;
        delete.ProfileId = GrantedProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var add = CreatePageModel(api, session: session);
        add.UserId = user.Id;
        add.ProfileId = GrantedProfileId;
        add.RoleId = ReviewerRoleId;
        await add.OnPostSaveAccessAsync(CancellationToken.None);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        var profile = Assert.Single(next.Access!.Profiles);
        Assert.Equal("Anthrax", profile.Title);
        Assert.Equal(ReviewerRoleId, profile.RoleId);
    }

    [Fact]
    public async Task OnPostSave_AppliesEveryStagedChangeAndClearsThem()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var add = CreatePageModel(api, session: session);
        add.UserId = user.Id;
        add.ProfileId = AvailableProfileId;
        add.RoleId = ReviewerRoleId;
        await add.OnPostSaveAccessAsync(CancellationToken.None);
        var delete = CreatePageModel(api, session: session);
        delete.UserId = user.Id;
        delete.ProfileId = GrantedProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var save = CreatePageModel(api, session: session);
        save.UserId = user.Id;
        save.SubscribedToReviewEmails = true;
        var result = await save.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(GrantedProfileId, api.DeletedProfileId);
        Assert.Equal(ReviewerRoleId, api.SavedAccess!.RoleId);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);
        Assert.False(next.HasPendingChanges);
    }

    [Fact]
    public async Task OnPostSave_KeepsChangesAndShowsError_WhenAStagedRemovalFails()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant(), DeleteSucceeds = false };
        var session = new FakeSession();
        var delete = CreatePageModel(api, session: session);
        delete.UserId = user.Id;
        delete.ProfileId = GrantedProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var save = CreatePageModel(api, session: session);
        save.UserId = user.Id;
        var result = await save.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(save.SaveError);
        Assert.True(save.HasPendingChanges);
    }

    [Fact]
    public async Task OnPostSave_ShowsApiMessage_WhenAStagedGrantIsRejected()
    {
        var user = User();
        var api = new FakeExternalUsersApiService
        {
            Users = [user],
            Access = AccessWithOneGrant(),
            AccessResult = new ExternalUserAccessResult(false, "Rejected by the API.")
        };
        var session = new FakeSession();
        var add = CreatePageModel(api, session: session);
        add.UserId = user.Id;
        add.ProfileId = AvailableProfileId;
        add.RoleId = ReviewerRoleId;
        await add.OnPostSaveAccessAsync(CancellationToken.None);

        var save = CreatePageModel(api, session: session);
        save.UserId = user.Id;
        var result = await save.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Rejected by the API.", save.SaveError);
    }

    [Fact]
    public async Task OnPostSave_ShowsError_WhenAStagedGrantThrows()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant(), ThrowOnSaveAccess = new HttpRequestException("down") };
        var session = new FakeSession();
        var add = CreatePageModel(api, session: session);
        add.UserId = user.Id;
        add.ProfileId = AvailableProfileId;
        add.RoleId = ReviewerRoleId;
        await add.OnPostSaveAccessAsync(CancellationToken.None);

        var save = CreatePageModel(api, session: session);
        save.UserId = user.Id;
        var result = await save.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("We could not save your changes. Try again later.", save.SaveError);
    }

    [Fact]
    public async Task OnGet_DiscardsStagedChanges_WhenAskedTo()
    {
        var user = User();
        var api = new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var delete = CreatePageModel(api, session: session);
        delete.UserId = user.Id;
        delete.ProfileId = GrantedProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        next.DiscardPending = true;
        await next.OnGetAsync(CancellationToken.None);

        Assert.False(next.HasPendingChanges);
        Assert.Single(next.Access!.Profiles);
    }

    [Fact]
    public async Task OnGet_IgnoresChangesStagedForAnotherUser()
    {
        var user = User();
        var other = User();
        var api = new FakeExternalUsersApiService { Users = [user, other], Access = AccessWithOneGrant() };
        var session = new FakeSession();
        var delete = CreatePageModel(api, session: session);
        delete.UserId = other.Id;
        delete.ProfileId = GrantedProfileId;
        await delete.OnPostDeleteAccessAsync(CancellationToken.None);

        var next = CreatePageModel(api, session: session);
        next.EditUserId = user.Id;
        await next.OnGetAsync(CancellationToken.None);

        Assert.False(next.HasPendingChanges);
    }

    [Fact]
    public async Task OnPostSave_ShowsError_WhenApiThrows()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], ThrowOnSetSubscription = new HttpRequestException("down") });
        model.UserId = user.Id;

        var result = await model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.SaveError);
    }

    [Fact]
    public async Task OnPostSaveAccess_Redirects_WhenUserIsNotInTheList()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [User()] });
        model.UserId = Guid.NewGuid();

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnPostSaveAccess_ShowsError_WhenRolesCannotBeLoaded()
    {
        var user = User();
        var model = CreatePageModel(
            new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() },
            new HttpRequestException("down"));
        model.UserId = user.Id;
        model.ProfileId = AvailableProfileId;
        model.RoleId = ContributorRoleId;

        var result = await model.OnPostSaveAccessAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("We could not save your changes. Try again later.", model.AccessError);
    }

    [Fact]
    public async Task OnPostDeleteAccess_StagesNothing_WhenTheProfileIsNotGranted()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService { Access = AccessWithOneGrant() });
        model.UserId = Guid.NewGuid();
        model.ProfileId = Guid.NewGuid();

        var result = await model.OnPostDeleteAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(model.RemoveErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAccess_SetsError_WhenApiThrows()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService { ThrowOnGetAccess = new HttpRequestException("down") });
        model.UserId = Guid.NewGuid();

        var result = await model.OnPostDeleteAccessAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("We could not remove this profile access. Try again later.", model.RemoveErrorMessage);
    }

    [Fact]
    public async Task OnGet_FlagsLoadError_WhenAccessCannotBeFetched()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], ThrowOnGetAccess = new HttpRequestException("down") });
        model.EditUserId = user.Id;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.HasAccessLoadError);
        Assert.Null(model.Access);
    }

    [Fact]
    public async Task OnGet_FlagsLoadError_WhenAccessIsNotFound()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = null });
        model.EditUserId = user.Id;
        model.AddProfileId = AvailableProfileId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.HasAccessLoadError);
        Assert.False(model.IsAccessPanelOpen);
    }

    [Fact]
    public async Task OnGet_ClosesAccessPanel_WhenRolesCannotBeLoaded()
    {
        var user = User();
        var model = CreatePageModel(
            new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() },
            new HttpRequestException("down"));
        model.EditUserId = user.Id;
        model.AccessProfileId = GrantedProfileId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.HasAccessLoadError);
        Assert.Null(model.EditingGrant);
        Assert.Null(model.AddingProfile);
    }

    [Fact]
    public async Task OnGet_RequestsEveryUser_WhenPageSizeIsAll()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [User()] });
        model.PageSize = "All";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Single(model.Users);
    }

    [Fact]
    public async Task OnPostSendInstructions_SendsToTheUser_AndRedirectsBackToTheSameListPositionWithASuccessMessage()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);
        var userId = Guid.NewGuid();
        model.UserId = userId;
        model.PageNumber = 3;
        model.PageSize = "20";

        var result = await model.OnPostSendInstructionsAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(userId, api.InstructionsSentTo);
        Assert.Equal("Login instructions sent", model.SuccessMessage);
        Assert.Null(model.SendInstructionsError);
        Assert.Equal(3, redirect.RouteValues!["PageNumber"]);
        Assert.Equal("20", redirect.RouteValues["PageSize"]);
    }

    [Fact]
    public async Task OnPostSendInstructions_ShowsTheApiMessage_WhenTheInstructionsCannotBeSent()
    {
        var api = new FakeExternalUsersApiService { SendInstructionsResult = new ExternalUserAccessResult(false, "The login instructions have already been sent to this user.") };
        var model = CreatePageModel(api);

        var result = await model.OnPostSendInstructionsAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("The login instructions have already been sent to this user.", model.SendInstructionsError);
        Assert.Null(model.SuccessMessage);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task OnPostSendInstructions_ShowsAGenericError_WhenTheApiCannotBeReached(Type exceptionType)
    {
        var api = new FakeExternalUsersApiService { ThrowOnSendInstructions = (Exception)Activator.CreateInstance(exceptionType)! };
        var model = CreatePageModel(api);

        var result = await model.OnPostSendInstructionsAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("We could not send the login instructions. Try again later.", model.SendInstructionsError);
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostPreRegister_SendsTrimmedDetails_AndRedirectsWithSuccessMessage()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);
        model.NewUser = new ExternalUsersModel.PreRegisterInput { FullName = " Jane Doe ", EmailAddress = " jane@example.test ", Organisation = " Org " };

        var result = await model.OnPostPreRegisterAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Jane Doe", api.PreRegistered!.FullName);
        Assert.Equal("jane@example.test", api.PreRegistered.EmailAddress);
        Assert.Equal("Org", api.PreRegistered.Organisation);
        Assert.Equal("User added successfully", model.SuccessMessage);
        Assert.Null(model.PreRegisterError);
    }

    [Fact]
    public async Task OnPostPreRegister_ShowsTheApiMessage_AndKeepsTheFormOpen_WhenRejected()
    {
        var api = new FakeExternalUsersApiService { Users = [User()], PreRegisterResult = new ExternalUserAccessResult(false, "A user with this email address already exists.") };
        var model = CreatePageModel(api);
        model.NewUser = new ExternalUsersModel.PreRegisterInput { FullName = "Jane Doe", EmailAddress = "jane@example.test", Organisation = "Org" };

        var result = await model.OnPostPreRegisterAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ShowPreRegisterForm);
        Assert.Equal("A user with this email address already exists.", model.PreRegisterError);
        Assert.Null(model.SuccessMessage);
        Assert.Single(model.Users);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task OnPostPreRegister_ShowsAGenericError_WhenTheApiCannotBeReached(Type exceptionType)
    {
        var api = new FakeExternalUsersApiService { ThrowOnPreRegister = (Exception)Activator.CreateInstance(exceptionType)! };
        var model = CreatePageModel(api);
        model.NewUser = new ExternalUsersModel.PreRegisterInput { FullName = "Jane Doe", EmailAddress = "jane@example.test", Organisation = "Org" };

        var result = await model.OnPostPreRegisterAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("We could not register this user. Try again later.", model.PreRegisterError);
    }

    [Fact]
    public async Task OnPostPreRegister_DoesNotCallTheApi_WhenAFieldIsInvalid()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);
        model.ModelState.AddModelError("NewUser.EmailAddress", "Enter the email address");

        var result = await model.OnPostPreRegisterAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(api.PreRegistered);
        Assert.True(model.ShowPreRegisterForm);
        Assert.Equal("Enter the email address", model.GetPreRegisterFieldError(nameof(ExternalUsersModel.PreRegisterInput.EmailAddress)));
    }

    [Fact]
    public async Task OnPostPreRegister_IgnoresErrorsOnOtherFields()
    {
        var api = new FakeExternalUsersApiService();
        var model = CreatePageModel(api);
        model.ModelState.AddModelError("UserId", "unrelated");
        model.NewUser = new ExternalUsersModel.PreRegisterInput { FullName = "Jane Doe", EmailAddress = "jane@example.test", Organisation = "Org" };

        var result = await model.OnPostPreRegisterAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(api.PreRegistered);
    }

    [Fact]
    public void GetPreRegisterFieldError_ReturnsNull_WhenTheFieldIsValid()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());

        Assert.Null(model.GetPreRegisterFieldError(nameof(ExternalUsersModel.PreRegisterInput.FullName)));
    }

    [Fact]
    public void BuildClosePreRegisterUrl_DelegatesToTheUrlHelper()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());
        model.Url = new StubUrlHelper("/UserAdmin/ExternalUsers?PageSize=10");

        Assert.Equal("/UserAdmin/ExternalUsers?PageSize=10", model.BuildClosePreRegisterUrl());
    }

    [Fact]
    public void UrlBuilders_DelegateToTheUrlHelper()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());
        model.Url = new StubUrlHelper("/UserAdmin/ExternalUsers?x=1");

        Assert.Equal("/UserAdmin/ExternalUsers?x=1", model.BuildPageUrl(2));
        Assert.Equal("/UserAdmin/ExternalUsers?x=1", model.BuildEditUrl(Guid.NewGuid()));
        Assert.Equal("/UserAdmin/ExternalUsers?x=1", model.BuildAccessUrl(GrantedProfileId));
        Assert.Equal("/UserAdmin/ExternalUsers?x=1", model.BuildCloseAccessUrl());
        Assert.Equal("/UserAdmin/ExternalUsers?x=1", model.BuildCloseUrl());
    }

    [Fact]
    public void ProfileUrlBuilders_PassTheProfilePagingAndSortToTheUrlHelper()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());
        var url = new StubUrlHelper("/UserAdmin/ExternalUsers");
        model.Url = url;
        model.ProfileSortDirection = "Ascending";

        model.BuildProfilePageUrl(4);
        Assert.Equal(4, url.LastValues!["ProfilePageNumber"]);

        model.BuildProfileSortUrl();
        Assert.Equal("Descending", url.LastValues["ProfileSortDirection"]);

        model.ProfileSortDirection = "Descending";
        model.BuildProfileSortUrl();
        Assert.Equal("Ascending", url.LastValues["ProfileSortDirection"]);
    }

    [Fact]
    public void PagedProfiles_IsEmpty_WhenNoAccessIsLoaded()
    {
        var model = CreatePageModel(new FakeExternalUsersApiService());

        Assert.Empty(model.PagedProfiles);
        Assert.Equal(1, model.ProfileTotalPages);
    }

    [Theory]
    [InlineData("Ascending", "ascending", "Anthrax")]
    [InlineData("Descending", "descending", "Brucellosis")]
    public async Task PagedProfiles_SortsByTitleInTheRequestedDirection(string direction, string ariaSort, string expectedFirst)
    {
        var user = User();
        var single = AccessWithOneGrant();
        var access = new ExternalUserProfileAccessContract
        {
            Profiles =
            [
                .. single.Profiles,
                new ExternalUserProfileGrantContract { ProfileId = Guid.NewGuid(), Title = "Brucellosis", RoleId = ReviewerRoleId, RoleName = "Profile reviewer" }
            ],
            AvailableProfiles = single.AvailableProfiles
        };
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = access });
        model.EditUserId = user.Id;
        model.ProfileSortDirection = direction;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(ariaSort, model.ProfileTitleAriaSort);
        Assert.Equal(expectedFirst, model.PagedProfiles[0].Title);
    }

    [Fact]
    public async Task PagedProfiles_ReturnsEveryProfile_WhenProfilePageSizeIsAll()
    {
        var user = User();
        var model = CreatePageModel(new FakeExternalUsersApiService { Users = [user], Access = AccessWithOneGrant() });
        model.EditUserId = user.Id;
        model.ProfilePageSize = "All";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Single(model.PagedProfiles);
        Assert.Equal(1, model.ProfileTotalPages);
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class StubUrlHelper(string result) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new() { RouteData = new Microsoft.AspNetCore.Routing.RouteData() };

        public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext) => result;

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => result;

        public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext)
        {
            LastValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary(routeContext.Values);

            return result;
        }

        public Microsoft.AspNetCore.Routing.RouteValueDictionary? LastValues { get; private set; }
    }
}
