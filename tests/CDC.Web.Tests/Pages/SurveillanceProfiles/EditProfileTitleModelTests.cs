using CDC.Web.Infrastructure;
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

public class EditProfileTitleModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private static EditProfileTitleModel CreatePageModel(
        ProfileAttributesDto? profileAttributes = null,
        Exception? throwOnGetProfileAttributes = null,
        UpdateProfileTitleResult? updateProfileTitleResult = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new EditProfileTitleModel(
            new FakeApiClient(
                profileAttributes: profileAttributes,
                throwOnGetProfileAttributes: throwOnGetProfileAttributes,
                updateProfileTitleResult: updateProfileTitleResult),
            new AlwaysEnabledLogger<EditProfileTitleModel>())
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

    private static ProfileAttributesDto Attributes(string title) => new()
    {
        Id = ProfileId,
        Title = title,
        LastUpdated = RowVersion
    };

    [Fact]
    public async Task OnGetAsync_PopulatesTitleAndLastUpdated_WhenTheProfileExists()
    {
        var pageModel = CreatePageModel(Attributes("Bovine tuberculosis"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Bovine tuberculosis", pageModel.ProfileTitle);
        Assert.Equal(Convert.ToBase64String(RowVersion), pageModel.LastUpdated);
        Assert.True(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(profileAttributes: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithModelError_WhenLoadingTheProfileFails()
    {
        var pageModel = CreatePageModel(throwOnGetProfileAttributes: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains(
            pageModel.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Unable to load the profile. Please try again.");
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPage_WhenModelStateIsInvalid()
    {
        var pageModel = CreatePageModel();
        pageModel.ModelState.AddModelError(nameof(EditProfileTitleModel.ProfileTitle), "You must enter a profile title");

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPageWithModelError_WhenLastUpdatedIsNotValidBase64()
    {
        var pageModel = CreatePageModel();
        pageModel.ProfileTitle = "Bovine tuberculosis";
        pageModel.LastUpdated = "not-base64!!";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(
            pageModel.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "The page has expired. Reload the profile and try again.");
    }

    [Fact]
    public async Task OnPostAsync_SetsStatusMessage_AndReloadsTheProfile_OnSuccess()
    {
        var pageModel = CreatePageModel(
            Attributes("Bovine tuberculosis (updated)"),
            updateProfileTitleResult: new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null));
        pageModel.ProfileTitle = "Bovine tuberculosis (updated)";
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("The profile title was successfully updated", pageModel.StatusMessage);
        Assert.Equal("Bovine tuberculosis (updated)", pageModel.ProfileTitle);
    }

    [Theory]
    [InlineData(UpdateProfileTitleOutcome.Conflict)]
    [InlineData(UpdateProfileTitleOutcome.ValidationFailed)]
    [InlineData(UpdateProfileTitleOutcome.Error)]
    public async Task OnPostAsync_ReturnsPageWithModelError_WhenTheApiCallDoesNotSucceed(UpdateProfileTitleOutcome outcome)
    {
        var pageModel = CreatePageModel(
            updateProfileTitleResult: new UpdateProfileTitleResult(outcome, "Something went wrong"));
        pageModel.ProfileTitle = "Bovine tuberculosis";
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Contains(
            pageModel.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Profile save failed: Something went wrong");
    }
}
