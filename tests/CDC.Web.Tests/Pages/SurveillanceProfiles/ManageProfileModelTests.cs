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
        Exception? throwOnGetManageProfile = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new ManageProfileModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            NullLogger<ManageProfileModel>.Instance)
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

    private static ManageProfileViewModel Profile() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Bovine Tuberculosis",
        ScenarioTitle = "Default Scenario",
        LatestPublishedVersionPublic = "Version 5",
        LatestPublishedVersionDefraNetOnly = "Version 7",
        LatestDraftVersion = "Version 8",
        ProfileStatus = "Draft"
    };

    [Fact]
    public async Task OnGetAsync_PopulatesProfile_WhenTheProfileExists()
    {
        var pageModel = CreatePageModel(Profile());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.Profile);
        Assert.Equal("Bovine Tuberculosis", pageModel.Profile!.ProfileTitle);
        Assert.Equal("Draft", pageModel.Profile.ProfileStatus);
        Assert.False(pageModel.HasError);
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
}
