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
        UpdateProfileTitleResult? updateProfileTitleResult = null) =>
        CreatePageModel(new FakeApiClient(
            profileAttributes: profileAttributes,
            throwOnGetProfileAttributes: throwOnGetProfileAttributes,
            updateProfileTitleResult: updateProfileTitleResult));

    private static EditProfileTitleModel CreatePageModel(FakeApiClient apiClient)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new EditProfileTitleModel(apiClient, new AlwaysEnabledLogger<EditProfileTitleModel>())
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
    [InlineData(UpdateProfileTitleOutcome.Error)]
    public async Task OnPostAsync_ReturnsPageWithGeneralModelError_WhenTheApiCallFailsForAnotherReason(UpdateProfileTitleOutcome outcome)
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

    [Fact]
    public async Task OnPostAsync_ShowsAnApiRefusalAgainstTheTitle_SoTheUserCanSeeWhichFieldToChange()
    {
        var pageModel = CreatePageModel(
            updateProfileTitleResult: new UpdateProfileTitleResult(
                UpdateProfileTitleOutcome.ValidationFailed, "The profile title is already in use for a different profile"));
        pageModel.ProfileTitle = "Bovine tuberculosis";
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.StatusMessage);
        Assert.Equal(
            "The profile title is already in use for a different profile",
            Assert.Single(pageModel.ModelState[nameof(EditProfileTitleModel.ProfileTitle)]!.Errors).ErrorMessage);
        Assert.False(pageModel.ModelState.ContainsKey(string.Empty));
    }

    // Markup alone shows nothing, so it is not a title - the editor leaves a <br> or &nbsp; behind when its text is deleted.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<br>")]
    [InlineData("&nbsp;&nbsp;")]
    [InlineData("<i> </i>")]
    [InlineData("<em></em>")]
    public async Task OnPostAsync_RejectsATitleWithNoText_AndDoesNotSaveIt(string? title)
    {
        var apiClient = new FakeApiClient(updateProfileTitleResult: new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null));
        var pageModel = CreatePageModel(apiClient);
        pageModel.ProfileTitle = title;
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(apiClient.LastUpdatedProfileTitle);
        Assert.Null(pageModel.StatusMessage);
        Assert.Equal(string.Empty, pageModel.ProfileTitle);
        Assert.Equal(
            "You must enter a profile title",
            Assert.Single(pageModel.ModelState[nameof(EditProfileTitleModel.ProfileTitle)]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_RejectsATitleOver255Characters_AndDoesNotSaveIt()
    {
        var apiClient = new FakeApiClient(updateProfileTitleResult: new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null));
        var pageModel = CreatePageModel(apiClient);
        pageModel.ProfileTitle = new string('a', 256);
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(apiClient.LastUpdatedProfileTitle);
        Assert.Equal(
            "Profile title cannot exceed 255 characters",
            Assert.Single(pageModel.ModelState[nameof(EditProfileTitleModel.ProfileTitle)]!.Errors).ErrorMessage);
    }

    // The limit applies to the title as stored, formatting included, as in the legacy page.
    [Fact]
    public async Task OnPostAsync_CountsTheFormattingMarkupTowardsTheLimit()
    {
        var pageModel = CreatePageModel();
        pageModel.ProfileTitle = $"<i>{new string('a', 250)}</i>";
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        await pageModel.OnPostAsync(CancellationToken.None);

        Assert.Contains(
            pageModel.ModelState[nameof(EditProfileTitleModel.ProfileTitle)]!.Errors,
            error => error.ErrorMessage == "Profile title cannot exceed 255 characters");
    }

    [Fact]
    public async Task OnPostAsync_TrimsTheTitleBeforeMeasuringItAndBeforeSavingIt()
    {
        var apiClient = new FakeApiClient(
            profileAttributes: Attributes(new string('a', 255)),
            updateProfileTitleResult: new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null));
        var pageModel = CreatePageModel(apiClient);
        pageModel.ProfileTitle = $"  {new string('a', 255)}  ";
        pageModel.LastUpdated = Convert.ToBase64String(RowVersion);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(new string('a', 255), apiClient.LastUpdatedProfileTitle);
        Assert.Equal("The profile title was successfully updated", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheForm_ForACurrentSituationProfile()
    {
        var pageModel = CreatePageModel(Attributes("Bovine tuberculosis"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.ShowForm);
    }

    [Fact]
    public async Task OnGetAsync_RefusesAWhatIfScenario_WhoseTitleCannotBeEdited()
    {
        var pageModel = CreatePageModel(Attributes("Bovine tuberculosis") with { ParentId = Guid.NewGuid() });

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowForm);
        Assert.Contains(
            pageModel.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "You cannot edit the profile title of a \"what-if\" scenario.");
    }
}
