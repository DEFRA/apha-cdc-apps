using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileContributionsModelTests
{
    private static EditProfileContributionsModel CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileSectionContributionDto>? contributions = null,
        Exception? throwOnGetProfileVersionSectionContributions = null) =>
        Configure(new EditProfileContributionsModel(
            new FakeApiClient(manageProfile: manageProfile),
            new FakeProfileSectionsApiService(
                metadata: TwoSectionMetadata(),
                contributions: contributions,
                throwOnGetProfileVersionSectionContributions: throwOnGetProfileVersionSectionContributions),
            new AlwaysEnabledLogger<EditProfileContributionsModel>()));

    [Fact]
    public void ActiveTab_IsContributions() =>
        Assert.Equal(ProfileSectionTab.Contributions, CreatePageModel(Profile()).ActiveTab);

    [Fact]
    public async Task OnGetAsync_LoadsContributions()
    {
        var contributions = new List<ProfileSectionContributionDto>
        {
            new()
            {
                FullName = "Jane Doe",
                UserName = "jdoe",
                OrganisationName = "APHA",
                LastContributionDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var pageModel = CreatePageModel(DraftProfile(), contributions);

        await pageModel.OnGetAsync(CancellationToken.None);

        var contribution = Assert.Single(pageModel.Contributions);
        Assert.Equal("Jane Doe", contribution.FullName);
        Assert.Equal("jdoe", contribution.UserName);
        Assert.Equal("APHA", contribution.OrganisationName);
        Assert.Equal("11/09/2026", contribution.LastContributionDate);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheUserCannotViewContributions()
    {
        var contributions = new List<ProfileSectionContributionDto> { new() { FullName = "Jane Doe" } };

        var pageModel = CreatePageModel(Profile(), contributions);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(pageModel.Contributions);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotFail_WhenLoadingContributionsThrows()
    {
        var pageModel = CreatePageModel(DraftProfile(), throwOnGetProfileVersionSectionContributions: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.Contributions);
    }
}
