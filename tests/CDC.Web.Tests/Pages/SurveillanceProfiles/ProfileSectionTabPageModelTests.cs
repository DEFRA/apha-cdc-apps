using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>Tests the behaviour every tab page inherits, through the References page.</summary>
public class ProfileSectionTabPageModelTests
{
    private static EditProfileReferencesModel CreatePageModel(ManageProfileViewModel profile, Guid? section = null) =>
        Configure(
            new EditProfileReferencesModel(
                new FakeApiClient(manageProfile: profile),
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()),
            section: section);

    [Fact]
    public async Task BuildTabs_ListsEveryTab_InLegacyOrder_WithTheSectionNameAsTheFirst_AndMarksTheActiveOne()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        var tabs = pageModel.BuildTabs();

        Assert.Equal(
            ["Summary", "References", "Further information", "Review comments", "All Revision dates", "Contributions"],
            tabs.Tabs.Select(tab => tab.Text));
        Assert.Equal(ProfileSectionTab.References, Assert.Single(tabs.Tabs, tab => tab.IsActive).Tab);
        Assert.Null(tabs.SubmitUrl);
    }

    [Fact]
    public async Task BuildTabs_NamesTheFirstTabAfterTheSection_InLegacyFormattedShortNameStyle()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Summary", pageModel.BuildTabs().Tabs[0].Text);
    }

    [Fact]
    public async Task BuildTabs_LinksEachTabToItsOwnPage_KeepingTheProfileAndSection()
    {
        var pageModel = CreatePageModel(DraftProfile(), section: EpidemiologySectionId);
        await pageModel.OnGetAsync(CancellationToken.None);

        var tabs = pageModel.BuildTabs().Tabs;

        Assert.Equal($"/SurveillanceProfiles/EditProfileQuestions?profileId={ProfileId}&section={EpidemiologySectionId}", tabs[0].Url);
        Assert.Equal($"/SurveillanceProfiles/EditProfileReferences?profileId={ProfileId}&section={EpidemiologySectionId}", tabs[1].Url);
        Assert.Equal($"/SurveillanceProfiles/EditProfileAllRevisionDates?profileId={ProfileId}&section={EpidemiologySectionId}", tabs[4].Url);
    }

    [Fact]
    public async Task BuildTabs_HidesTheTabsTheUserCannotSee()
    {
        var pageModel = CreatePageModel(Profile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            [ProfileSectionTab.Questions, ProfileSectionTab.References, ProfileSectionTab.FurtherInformation],
            pageModel.BuildTabs().Tabs.Select(tab => tab.Tab));
    }

    [Fact]
    public async Task BuildTabs_PostsToTheNamedHandler_WhenGivenOne()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        var tabs = pageModel.BuildTabs("NavigateTab");

        Assert.Equal($"/SurveillanceProfiles/EditProfileReferences?profileId={ProfileId}&section={SummarySectionId}&handler=NavigateTab", tabs.SubmitUrl);
    }

    [Fact]
    public void BuildTabs_Throws_WhenNoSectionIsSelected() =>
        Assert.Throws<InvalidOperationException>(() => CreatePageModel(Profile()).BuildTabs());

    [Fact]
    public async Task BuildSectionUrl_AlwaysOpensTheSectionsQuestionsTab_AsLegacyProfileSectionLinksDid()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            $"/SurveillanceProfiles/EditProfileQuestions?profileId={ProfileId}&section={EpidemiologySectionId}",
            pageModel.BuildSectionUrl(EpidemiologySectionId));
    }

    [Fact]
    public async Task BuildPaginationUrl_StaysOnTheCurrentTab_AsLegacySectionPaginationDid()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            $"/SurveillanceProfiles/EditProfileReferences?profileId={ProfileId}&section={EpidemiologySectionId}",
            pageModel.BuildPaginationUrl(EpidemiologySectionId));
    }

    [Fact]
    public async Task BuildPrintUrl_OpensThePrintPage_ForTheWholeProfileOrOneSection()
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.StartsWith($"/SurveillanceProfiles/ViewProfilePrintVersion?profileId={ProfileId}", pageModel.BuildPrintUrl());
        Assert.Equal(
            $"/SurveillanceProfiles/ViewProfilePrintVersion?profileId={ProfileId}&section={EpidemiologySectionId}",
            pageModel.BuildPrintUrl(EpidemiologySectionId));
    }

    [Theory]
    [InlineData(DraftSummaryReport.SummaryProfile, "SummaryProfile")]
    [InlineData(DraftSummaryReport.SummaryPrioritisation, "SummaryPrioritisation")]
    public async Task BuildSummaryReportUrl_OpensTheReportPage_NamingTheReport(DraftSummaryReport report, string routeName)
    {
        var pageModel = CreatePageModel(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal($"/SurveillanceProfiles/ViewDraftSummaryReport?profileId={ProfileId}&report={routeName}", pageModel.BuildSummaryReportUrl(report));
    }

    [Fact]
    public async Task IsReady_IsFalse_WhenTheProfileHasNoVersion()
    {
        var pageModel = CreatePageModel(Profile(currentProfileVersionId: Guid.Empty));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.IsReady);
        Assert.True(pageModel.HasNoVersion);
        Assert.NotNull(pageModel.CurrentSection);
    }

    [Fact]
    public async Task IsReady_IsTrue_WhenTheProfileSectionAndVersionLoad()
    {
        var pageModel = CreatePageModel(Profile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.IsReady);
        Assert.Equal(ProfileVersionId, pageModel.CurrentProfileVersionId);
    }

    [Fact]
    public async Task OnGetAsync_SendsTheUserToTheApplicationErrorPage_WhenCdcApiRefusesThemTheProfile()
    {
        var pageModel = Configure(
            new EditProfileReferencesModel(
                new FakeApiClient(throwOnGetManageProfile: Refused()),
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()));
        var tempData = WithTempData(pageModel);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        AssertSentToTheApplicationErrorPage(result, tempData);
        Assert.False(pageModel.IsReady);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheLoadError_WhenCdcApiFailsToLoadTheProfileForAnyOtherReason()
    {
        var pageModel = Configure(
            new EditProfileReferencesModel(
                new FakeApiClient(throwOnGetManageProfile: new HttpRequestException("boom", null, HttpStatusCode.InternalServerError)),
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_NamesTheVersionTheUserFollowedALinkTo_SoCdcApiCanRefuseADraftTheyMayNotRead()
    {
        var versionId = Guid.NewGuid();
        var apiClient = new FakeApiClient(manageProfile: Profile());
        var pageModel = Configure(
            new EditProfileReferencesModel(
                apiClient,
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()));
        pageModel.ProfileVersionId = versionId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(versionId, apiClient.LastManageProfileVersionId);
    }

    [Fact]
    public async Task OnGetAsync_NamesNoVersion_WhenTheUserDidNotFollowAVersionsLink()
    {
        var apiClient = new FakeApiClient(manageProfile: Profile());
        var pageModel = Configure(
            new EditProfileReferencesModel(
                apiClient,
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(apiClient.LastManageProfileVersionId);
    }

    [Fact]
    public async Task OnGetAsync_SendsTheUserToTheApplicationErrorPage_WhenCdcApiRefusesTheVersionTheyFollowedALinkTo()
    {
        var pageModel = Configure(
            new EditProfileReferencesModel(
                new FakeApiClient(manageProfile: Profile(), throwOnGetManageProfileForAVersion: Refused()),
                new FakeProfileSectionsApiService(metadata: TwoSectionMetadata()),
                new AlwaysEnabledLogger<EditProfileReferencesModel>()));
        pageModel.ProfileVersionId = Guid.NewGuid();
        var tempData = WithTempData(pageModel);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        AssertSentToTheApplicationErrorPage(result, tempData);
    }

    [Fact]
    public async Task HasFormErrors_IsFalse_ForATabThatReportsNoErrors()
    {
        var pageModel = CreatePageModel(Profile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task ListPage_IsTheOnlyPage_ForATabThatDoesNotPaginate()
    {
        var pageModel = CreatePageModel(Profile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.ListPageNumber);
        Assert.Equal(1, pageModel.ListTotalPages);
    }
}
