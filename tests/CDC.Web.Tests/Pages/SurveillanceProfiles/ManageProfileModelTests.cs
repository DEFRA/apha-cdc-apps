using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Common.Contracts;
using CDC.Web.Features.Account;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ManageProfileModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private static ManageProfileModel CreatePageModel(
        ManageProfileViewModel? manageProfile = null,
        Exception? throwOnGetManageProfile = null,
        IReadOnlyList<ProfileStatusTypeDto>? profileStatusTypes = null,
        UpdateProfileStatusResult? updateProfileStatusResult = null,
        CreateNewProfileVersionResult? createNewProfileVersionResult = null,
        DeleteProfileVersionResult? deleteProfileVersionResult = null,
        PublishProfileResult? publishProfileResult = null) =>
        CreatePageModel(new FakeApiClient(
            manageProfile: manageProfile,
            throwOnGetManageProfile: throwOnGetManageProfile,
            profileStatusTypes: profileStatusTypes,
            updateProfileStatusResult: updateProfileStatusResult,
            createNewProfileVersionResult: createNewProfileVersionResult,
            deleteProfileVersionResult: deleteProfileVersionResult,
            publishProfileResult: publishProfileResult));

    private static ManageProfileModel CreatePageModel(FakeApiClient apiClient)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new ManageProfileModel(apiClient, new AlwaysEnabledLogger<ManageProfileModel>())
        {
            ProfileId = ProfileId,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        return pageModel;
    }

    private static readonly Guid DraftStatusId = Guid.NewGuid();
    private static readonly Guid ValidationCompleteStatusId = Guid.NewGuid();

    private static ManageProfileViewModel Profile() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Bovine Tuberculosis",
        ScenarioTitle = "Default Scenario",
        LatestPublishedVersionPublic = "Version 5",
        LatestPublishedVersionDefraNetOnly = "Version 7",
        LatestDraftVersion = "Version 8",
        ProfileStatus = "Draft",
        ProfileStatusId = DraftStatusId
    };

    private static IReadOnlyList<ProfileStatusTypeDto> StatusTypes() =>
    [
        new ProfileStatusTypeDto { Id = DraftStatusId, Name = "Draft" },
        new ProfileStatusTypeDto { Id = ValidationCompleteStatusId, Name = "Validation complete", IsValidationComplete = true }
    ];

    private static ClaimsPrincipal SignedInUser(bool isExternalUser)
    {
        List<Claim> claims = [new(ClaimTypes.Name, "test-user")];

        if (isExternalUser)
        {
            claims.Add(new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    [Fact]
    public async Task OnGetAsync_PopulatesProfile_WhenTheProfileExists()
    {
        var pageModel = CreatePageModel(Profile(), profileStatusTypes: StatusTypes());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.Profile);
        Assert.Equal("Bovine Tuberculosis", pageModel.Profile.ProfileTitle);
        Assert.Equal("Draft", pageModel.Profile.ProfileStatus);
        Assert.False(pageModel.HasError);
        Assert.Equal(2, pageModel.ProfileStatusTypes.Count);
        Assert.Equal(DraftStatusId, pageModel.ProfileStatusId);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(pageModel.Profile);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithHasError_WhenLoadingTheProfileFails()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.Profile);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithHasError_WhenLoadingTheProfileTimesOut()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new TaskCanceledException());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.Profile);
    }

    [Fact]
    public async Task OnPostAsync_UpdatesStatusAndShowsSuccessMessage_WhenTheSaveSucceeds()
    {
        var pageModel = CreatePageModel(
            Profile() with { ProfileStatus = "Validation complete", ProfileStatusId = ValidationCompleteStatusId },
            profileStatusTypes: StatusTypes(),
            updateProfileStatusResult: new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Success, null));
        pageModel.ProfileStatusId = ValidationCompleteStatusId;

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Successfully updated the profile status.", pageModel.StatusMessage);
        Assert.Null(pageModel.StatusErrorMessage);
        Assert.Equal(ValidationCompleteStatusId, pageModel.ProfileStatusId);
    }

    [Fact]
    public async Task OnPostAsync_ShowsErrorMessage_WhenTheSaveFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            updateProfileStatusResult: new UpdateProfileStatusResult(UpdateProfileStatusOutcome.NotFound, "The selected profile status could not be found."));
        pageModel.ProfileStatusId = ValidationCompleteStatusId;

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Equal("The selected profile status could not be found.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPageWithHasError_WhenTheReloadFails()
    {
        var pageModel = CreatePageModel(
            throwOnGetManageProfile: new HttpRequestException("connection refused"),
            updateProfileStatusResult: new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Success, null));
        pageModel.ProfileStatusId = ValidationCompleteStatusId;

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Equal("Successfully updated the profile status.", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsNotFound_WhenTheProfileNoLongerExistsAfterSaving()
    {
        var pageModel = CreatePageModel(
            manageProfile: null,
            updateProfileStatusResult: new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Success, null));
        pageModel.ProfileStatusId = ValidationCompleteStatusId;

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostCreateNewDraftVersionAsync_RedirectsToEditProfileQuestions_WhenSuccessful()
    {
        var newVersionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Success, newVersionId, null));

        var result = await pageModel.OnPostCreateNewDraftVersionAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/EditProfileQuestions", redirect.PageName);
        Assert.Equal(ProfileId, Assert.Single(redirect.RouteValues!).Value);
    }

    [Fact]
    public async Task OnPostCreateNewDraftVersionAsync_ReturnsPageWithError_WhenCreationFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(
                CreateNewProfileVersionOutcome.Conflict, null, "This profile version is not eligible for a new draft version."));

        var result = await pageModel.OnPostCreateNewDraftVersionAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("This profile version is not eligible for a new draft version.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostCreateNewDraftVersionAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostCreateNewDraftVersionAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostCreateNewDraftVersionAsync_ReturnsPageWithHasError_WhenLoadingTheProfileFails()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new HttpRequestException("connection refused"));

        var result = await pageModel.OnPostCreateNewDraftVersionAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    // Whether the whole profile went with its only version makes no difference: legacy always went to the home page.
    [Theory]
    [InlineData(false, false, "Internal")]
    [InlineData(false, true, "Internal")]
    [InlineData(true, false, "External")]
    [InlineData(true, true, "External")]
    public async Task OnPostDeleteCurrentVersionAsync_RedirectsToTheUsersHomePage_WhenSuccessful(
        bool isExternalUser, bool isProfileDeleted, string expectedAction)
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            deleteProfileVersionResult: new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Success, isProfileDeleted, null));
        pageModel.PageContext.HttpContext.User = SignedInUser(isExternalUser);

        var result = await pageModel.OnPostDeleteCurrentVersionAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(expectedAction, redirect.ActionName);
        Assert.Equal("Landing", redirect.ControllerName);
    }

    [Fact]
    public async Task OnPostDeleteCurrentVersionAsync_ReturnsPageWithError_WhenDeletionFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            deleteProfileVersionResult: new DeleteProfileVersionResult(
                DeleteProfileVersionOutcome.NotFound, false, "This profile version could not be found."));

        var result = await pageModel.OnPostDeleteCurrentVersionAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("This profile version could not be found.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteCurrentVersionAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostDeleteCurrentVersionAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostPublishPublicAsync_RedirectsToTheNewVersionsProfileReports_WhenSuccessful()
    {
        var newVersionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(PublishProfileOutcome.Success, newVersionId, null));

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/ProfileReports", redirect.PageName);
        Assert.Equal(newVersionId, Assert.Single(redirect.RouteValues!).Value);
        Assert.Equal("Successfully published this profile and made it public.", pageModel.SuccessMessage);
        Assert.Null(pageModel.StatusMessage);
        Assert.Null(pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishPublicAsync_ReturnsPageWithError_WhenPublishFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(
                PublishProfileOutcome.Conflict, null, "This profile version is not eligible for publishing."));

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Null(pageModel.SuccessMessage);
        Assert.Equal("This profile version is not eligible for publishing.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishPublicAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostPublishDefranetOnlyAsync_RedirectsToTheNewVersionsProfileReports_WhenSuccessful()
    {
        var newVersionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(PublishProfileOutcome.Success, newVersionId, null));

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/ProfileReports", redirect.PageName);
        Assert.Equal(newVersionId, Assert.Single(redirect.RouteValues!).Value);
        Assert.Equal("Successfully published this profile but did not make it public.", pageModel.SuccessMessage);
        Assert.Null(pageModel.StatusMessage);
        Assert.Null(pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishDefranetOnlyAsync_ReturnsPageWithError_WhenPublishFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(
                PublishProfileOutcome.Conflict, null, "This profile version is not eligible for publishing."));

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Null(pageModel.SuccessMessage);
        Assert.Equal("This profile version is not eligible for publishing.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishDefranetOnlyAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publish_AsksTheApiToPublishTheProfile_AsPublicOnlyForPublishPublic(bool isPublic)
    {
        var apiClient = new FakeApiClient(manageProfile: Profile(), profileStatusTypes: StatusTypes());
        var pageModel = CreatePageModel(apiClient);

        _ = isPublic
            ? await pageModel.OnPostPublishPublicAsync(CancellationToken.None)
            : await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.Equal(ProfileId, apiClient.PublishedProfileId);
        Assert.Equal(isPublic, apiClient.PublishedAsPublic);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publish_ListsTheValidationErrorsAndPublishesNothing_WhenTheDraftIsInvalid(bool isPublic)
    {
        ProfileSectionInvalidReasonDto[] reasons =
        [
            new() { SectionNumber = 1, QuestionNumber = 0, Description = "\"Revision dates, Next policy review\" is required" },
            new() { SectionNumber = 3, QuestionNumber = 2, Description = "\"Is it endemic? - Endemic\" is required" }
        ];
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(PublishProfileOutcome.InvalidProfile, null, null) { InvalidReasons = reasons });

        var result = isPublic
            ? await pageModel.OnPostPublishPublicAsync(CancellationToken.None)
            : await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(reasons, pageModel.InvalidReasons);
        Assert.Null(pageModel.SuccessMessage);
        Assert.Null(pageModel.StatusErrorMessage);
        Assert.NotNull(pageModel.Profile);
    }

    [Fact]
    public async Task OnGetAsync_ListsNoValidationErrors()
    {
        var pageModel = CreatePageModel(Profile(), profileStatusTypes: StatusTypes());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.InvalidReasons);
    }

    [Theory]
    [InlineData(PublishProfileOutcome.Forbidden, "You do not have permission to publish this profile.")]
    [InlineData(PublishProfileOutcome.Conflict, "You cannot publish this profile because it does not have a current draft version.")]
    [InlineData(PublishProfileOutcome.Conflict, "You cannot publish this scenario because the profile to which it belongs is not published.")]
    [InlineData(PublishProfileOutcome.Conflict, "You cannot make this scenario public because there is no public version of this profile.")]
    [InlineData(PublishProfileOutcome.Conflict, "Version 8.1 is not the latest version of this profile. The profile may have been altered by another user.")]
    [InlineData(PublishProfileOutcome.Error, "The profile could not be published. Please try again.")]
    public async Task Publish_ShowsTheMessageOfTheRuleThatWasBroken_AndListsNoValidationErrors(PublishProfileOutcome outcome, string message)
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            publishProfileResult: new PublishProfileResult(outcome, null, message));

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(message, pageModel.StatusErrorMessage);
        Assert.Empty(pageModel.InvalidReasons);
        Assert.Null(pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostDeleteCurrentVersionAsync_ReturnsPageWithHasError_WhenLoadingTheProfileFails()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new HttpRequestException("connection refused"));

        var result = await pageModel.OnPostDeleteCurrentVersionAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }
}

