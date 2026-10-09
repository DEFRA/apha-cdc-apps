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
        DeleteProfileVersionResult? deleteProfileVersionResult = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new ManageProfileModel(
            new FakeApiClient(
                manageProfile: manageProfile,
                throwOnGetManageProfile: throwOnGetManageProfile,
                profileStatusTypes: profileStatusTypes,
                updateProfileStatusResult: updateProfileStatusResult,
                createNewProfileVersionResult: createNewProfileVersionResult,
                deleteProfileVersionResult: deleteProfileVersionResult),
            new AlwaysEnabledLogger<ManageProfileModel>())
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

    [Fact]
    public async Task OnPostDeleteCurrentVersionAsync_RedirectsToLandingIndex_WhenSuccessful()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            deleteProfileVersionResult: new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Success, false, null));

        var result = await pageModel.OnPostDeleteCurrentVersionAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
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
    public async Task OnPostPublishPublicAsync_ShowsSuccessMessage_WhenSuccessful()
    {
        var newVersionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Success, newVersionId, null));

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Successfully published this profile and made it public.", pageModel.StatusMessage);
        Assert.Null(pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishPublicAsync_ReturnsPageWithError_WhenPublishFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(
                CreateNewProfileVersionOutcome.Conflict, null, "This profile version is not eligible for publishing."));

        var result = await pageModel.OnPostPublishPublicAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
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
    public async Task OnPostPublishDefranetOnlyAsync_ShowsSuccessMessage_WhenSuccessful()
    {
        var newVersionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Success, newVersionId, null));

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Successfully published this profile but did not make it public.", pageModel.StatusMessage);
        Assert.Null(pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishDefranetOnlyAsync_ReturnsPageWithError_WhenPublishFails()
    {
        var pageModel = CreatePageModel(
            Profile(),
            profileStatusTypes: StatusTypes(),
            createNewProfileVersionResult: new CreateNewProfileVersionResult(
                CreateNewProfileVersionOutcome.Conflict, null, "This profile version is not eligible for publishing."));

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Equal("This profile version is not eligible for publishing.", pageModel.StatusErrorMessage);
    }

    [Fact]
    public async Task OnPostPublishDefranetOnlyAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostPublishDefranetOnlyAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
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

