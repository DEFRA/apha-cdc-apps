using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileAllRevisionDatesModelTests
{
    private static readonly Guid YearlyFrequencyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompleteStatusId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private static ProfileVersionAllRevisionDatesDto RevisionDatesData() => new()
    {
        ProfileVersionId = ProfileVersionId,
        Sections =
        [
            new ProfileSectionRevisionDatesDto
            {
                ProfileSectionId = SummarySectionId,
                SectionName = "Summary",
                SectionNumber = 1,
                VaReviewFrequencyId = YearlyFrequencyId,
                NextVaReview = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AuthorReviewStatusId = CompleteStatusId,
                LastUpdated = RowVersion
            }
        ],
        ReviewFrequencies = [new ReviewLookupItemDto { Id = YearlyFrequencyId, Name = "Every year" }],
        ReviewStatuses = [new ReviewLookupItemDto { Id = CompleteStatusId, Name = "Complete" }]
    };

    private static (EditProfileAllRevisionDatesModel PageModel, FakeProfileSectionsApiService Service) CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IDictionary<string, string>? form = null,
        SaveRevisionDatesResult? saveResult = null,
        Exception? throwOnSave = null,
        Exception? throwOnGet = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: TwoSectionMetadata(),
            allRevisionDates: RevisionDatesData(),
            saveRevisionDatesResult: saveResult,
            throwOnSaveProfileVersionRevisionDates: throwOnSave,
            throwOnGetProfileVersionAllRevisionDates: throwOnGet);

        var pageModel = Configure(
            new EditProfileAllRevisionDatesModel(
                new FakeApiClient(manageProfile: manageProfile),
                service,
                new AlwaysEnabledLogger<EditProfileAllRevisionDatesModel>()),
            form);

        return (pageModel, service);
    }

    private static Dictionary<string, string> RevisionForm(string vaNext = "1 January 2027") => new()
    {
        [ProfileRevisionDatesForm.FieldName(SummarySectionId, ProfileRevisionDatesForm.FieldVaFrequency)] = YearlyFrequencyId.ToString(),
        [ProfileRevisionDatesForm.FieldName(SummarySectionId, ProfileRevisionDatesForm.FieldNextVa)] = vaNext,
        [ProfileRevisionDatesForm.FieldName(SummarySectionId, ProfileRevisionDatesForm.FieldAuthorStatus)] = CompleteStatusId.ToString(),
        [ProfileRevisionDatesForm.FieldName(SummarySectionId, ProfileRevisionDatesForm.FieldLastUpdated)] = "AQIDBAUGBwg="
    };

    [Fact]
    public void ActiveTab_IsAllRevisionDates() =>
        Assert.Equal(ProfileSectionTab.AllRevisionDates, CreatePageModel(Profile()).PageModel.ActiveTab);

    [Fact]
    public async Task OnGetAsync_LoadsEveryRevisionDate_AsAnEditableTab_WhenTheUserCanEdit()
    {
        var (pageModel, _) = CreatePageModel(DraftProfile());

        await pageModel.OnGetAsync(CancellationToken.None);

        var tab = Assert.IsType<RevisionDatesTabView>(pageModel.RevisionDatesTab);
        Assert.True(tab.IsEditable);
        var row = Assert.Single(tab.Rows);
        Assert.Equal("1. Summary", row.SectionDisplay);
        Assert.Equal(YearlyFrequencyId.ToString(), row.VaFrequency);
        Assert.Equal("1 January 2027", row.NextVaReview);
        Assert.Equal(CompleteStatusId.ToString(), row.AuthorStatus);
        Assert.Equal(string.Empty, row.PolicyFrequency);
        Assert.Equal("AQIDBAUGBwg=", row.LastUpdated);
        Assert.Equal(2, tab.FrequencyOptions.Count);
        Assert.Equal(string.Empty, tab.FrequencyOptions[0].Value);
        Assert.Equal("Please select...", tab.FrequencyOptions[0].Text);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheRevisionDatesReadOnly_WhenTheUserCannotEdit()
    {
        var (pageModel, _) = CreatePageModel(DraftProfile() with { CanEditProfileSection = false });

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(Assert.IsType<RevisionDatesTabView>(pageModel.RevisionDatesTab).IsEditable);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheUserCannotViewTheRevisionDates()
    {
        var (pageModel, service) = CreatePageModel(Profile());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(pageModel.RevisionDatesTab);
        Assert.Equal(0, service.GetAllRevisionDatesCallCount);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotFail_WhenLoadingTheRevisionDatesThrows()
    {
        var (pageModel, _) = CreatePageModel(DraftProfile(), throwOnGet: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Null(pageModel.RevisionDatesTab);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_SendsParsedValues_AndShowsTheSuccessMessage()
    {
        var (pageModel, service) = CreatePageModel(DraftProfile(), RevisionForm());

        var result = await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        var saved = Assert.Single(service.SavedRevisionDates!);
        Assert.Equal(SummarySectionId, saved.ProfileSectionId);
        Assert.Equal(YearlyFrequencyId, saved.VaReviewFrequencyId);
        Assert.Equal(new DateTime(2027, 1, 1), saved.NextVaReview);
        Assert.Equal(CompleteStatusId, saved.AuthorReviewStatusId);
        Assert.Null(saved.PolicyReviewFrequencyId);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, saved.LastUpdated);
        Assert.Equal(1, pageModel.RevisionDatesSavedCount);
        Assert.Empty(pageModel.RevisionDatesErrors);
        Assert.Null(pageModel.RevisionDatesSaveError);
        Assert.Equal(2, service.GetAllRevisionDatesCallCount);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_ReportsInvalidDates_KeepsTheTypedValue_AndDoesNotSave()
    {
        var (pageModel, service) = CreatePageModel(DraftProfile(), RevisionForm(vaNext: "not a date"));

        await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.Null(service.SavedRevisionDates);
        Assert.Equal("1. Summary: Next VA review date must be a valid date", Assert.Single(pageModel.RevisionDatesErrors));
        Assert.Null(pageModel.RevisionDatesSavedCount);
        Assert.Equal("not a date", Assert.Single(pageModel.RevisionDatesTab!.Rows).NextVaReview);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_ShowsTheLatestValues_WhenAnotherUserChangedTheSection()
    {
        var (pageModel, service) = CreatePageModel(
            DraftProfile(),
            RevisionForm(vaNext: "2 February 2028"),
            new SaveRevisionDatesResult { Outcome = SaveRevisionDatesOutcome.Conflict, ErrorMessage = "Another user has changed these revision dates." });

        await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.Equal("Profile section save failed: Another user has changed these revision dates.", pageModel.RevisionDatesSaveError);
        Assert.Null(pageModel.RevisionDatesSavedCount);
        Assert.Equal("1 January 2027", Assert.Single(pageModel.RevisionDatesTab!.Rows).NextVaReview);
        Assert.Equal(2, service.GetAllRevisionDatesCallCount);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_KeepsTheTypedValues_WhenCdcApiRejectsTheSubmission()
    {
        var (pageModel, _) = CreatePageModel(
            DraftProfile(),
            RevisionForm(vaNext: "2 February 2028"),
            new SaveRevisionDatesResult { Outcome = SaveRevisionDatesOutcome.ValidationFailed, ErrorMessage = "Not accepted." });

        await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.Equal("Profile section save failed: Not accepted.", pageModel.RevisionDatesSaveError);
        Assert.Equal("2 February 2028", Assert.Single(pageModel.RevisionDatesTab!.Rows).NextVaReview);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_ShowsAnError_WhenCdcApiThrows()
    {
        var (pageModel, _) = CreatePageModel(DraftProfile(), RevisionForm(), throwOnSave: new HttpRequestException("boom"));

        await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.StartsWith("Profile section save failed: ", pageModel.RevisionDatesSaveError, StringComparison.Ordinal);
        Assert.Null(pageModel.RevisionDatesSavedCount);
        Assert.Equal("1 January 2027", Assert.Single(pageModel.RevisionDatesTab!.Rows).NextVaReview);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_DoesNotSave_WhenTheUserCannotEdit()
    {
        var (pageModel, service) = CreatePageModel(DraftProfile() with { CanEditProfileSection = false }, RevisionForm());

        await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.Null(service.SavedRevisionDates);
        Assert.NotNull(pageModel.RevisionDatesSaveError);
    }

    [Fact]
    public async Task OnPostSaveRevisionDatesAsync_ReturnsNotFound_WhenTheUserCannotViewTheRevisionDates()
    {
        var (pageModel, service) = CreatePageModel(Profile(), RevisionForm());

        var result = await pageModel.OnPostSaveRevisionDatesAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(service.SavedRevisionDates);
    }
}
