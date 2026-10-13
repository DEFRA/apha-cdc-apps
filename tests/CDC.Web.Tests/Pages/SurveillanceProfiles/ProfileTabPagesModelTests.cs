using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileReferencesModelTests
{
    private static EditProfileReferencesModel CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByNoteType = null,
        Exception? throwOnGetProfileNoteTypes = null) =>
        Configure(new EditProfileReferencesModel(
            new FakeApiClient(manageProfile: manageProfile),
            new FakeProfileSectionsApiService(
                metadata: TwoSectionMetadata(),
                noteTypes: noteTypes,
                notesByNoteType: notesByNoteType,
                throwOnGetProfileNoteTypes: throwOnGetProfileNoteTypes),
            new AlwaysEnabledLogger<EditProfileReferencesModel>()));

    [Fact]
    public void ActiveTab_IsReferences() =>
        Assert.Equal(ProfileSectionTab.References, CreatePageModel(Profile()).ActiveTab);

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var result = await CreatePageModel(manageProfile: null).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesBothReferenceGroups_MatchedByNoteTypeName_RegardlessOfCasingOrSpacing()
    {
        var scientificPaperReferenceTypeId = Guid.NewGuid();
        var legislativeReferenceTypeId = Guid.NewGuid();

        var noteTypes = new List<ProfileNoteTypeDto>
        {
            new() { Id = scientificPaperReferenceTypeId, Name = "scientific paper reference", PluralName = "Scientific paper references" },
            new() { Id = legislativeReferenceTypeId, Name = "Legislative Reference", PluralName = "Legislative references" }
        };

        var notesByNoteType = new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
        {
            [scientificPaperReferenceTypeId] =
            [
                new ProfileNoteDto
                {
                    Id = Guid.NewGuid(),
                    NoteText = "Zebra et al. (2020) <a href='https://example.com'>https://example.com</a>",
                    QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = SummarySectionId, ProfileQuestionId = QuestionId }]
                },
                new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "Aardvark et al. (2019)" }
            ],
            [legislativeReferenceTypeId] = [new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "Animal Health Act 1981" }]
        };

        var pageModel = CreatePageModel(Profile(), noteTypes, notesByNoteType);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Scientific paper references", pageModel.ScientificPaperReferences.Heading);
        Assert.Equal(2, pageModel.ScientificPaperReferences.Notes.Count);
        Assert.Equal("Aardvark et al. (2019)", pageModel.ScientificPaperReferences.Notes[0].NoteTextHtml);
        Assert.Equal("1.1", pageModel.ScientificPaperReferences.Notes[1].QuestionReferenceDisplay);
        Assert.Contains("<a href=", pageModel.ScientificPaperReferences.Notes[1].NoteTextHtml);

        Assert.Equal("Legislative references", pageModel.LegislativeReferences.Heading);
        Assert.Single(pageModel.LegislativeReferences.Notes);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToEmptyGroups_WhenNoteTypesAreNotFound()
    {
        var pageModel = CreatePageModel(Profile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
        Assert.Equal("There are no scientific paper references to display.", pageModel.ScientificPaperReferences.EmptyMessage);
        Assert.Empty(pageModel.LegislativeReferences.Notes);
    }

    [Fact]
    public async Task OnGetAsync_StillReturnsThePage_WhenLoadingReferencesFails()
    {
        var pageModel = CreatePageModel(Profile(), throwOnGetProfileNoteTypes: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
        Assert.Empty(pageModel.LegislativeReferences.Notes);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotLoadReferences_WhenTheProfileHasNoVersion()
    {
        var pageModel = CreatePageModel(Profile(currentProfileVersionId: Guid.Empty));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.HasNoVersion);
        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
    }
}

public class EditProfileFurtherInformationModelTests
{
    private static EditProfileFurtherInformationModel CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByNoteType = null,
        Exception? throwOnGetProfileNoteTypes = null) =>
        Configure(new EditProfileFurtherInformationModel(
            new FakeApiClient(manageProfile: manageProfile),
            new FakeProfileSectionsApiService(
                metadata: TwoSectionMetadata(),
                noteTypes: noteTypes,
                notesByNoteType: notesByNoteType,
                throwOnGetProfileNoteTypes: throwOnGetProfileNoteTypes),
            new AlwaysEnabledLogger<EditProfileFurtherInformationModel>()));

    [Fact]
    public void ActiveTab_IsFurtherInformation() =>
        Assert.Equal(ProfileSectionTab.FurtherInformation, CreatePageModel(Profile()).ActiveTab);

    [Fact]
    public async Task OnGetAsync_PopulatesFurtherInformation_MatchedByNoteTypeName()
    {
        var furtherInformationTypeId = Guid.NewGuid();
        var noteTypes = new List<ProfileNoteTypeDto>
        {
            new() { Id = furtherInformationTypeId, Name = "SourceOfFurtherInformation", PluralName = "Sources of further information" }
        };
        var notesByNoteType = new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
        {
            [furtherInformationTypeId] = [new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "OIE Terrestrial Manual" }]
        };

        var pageModel = CreatePageModel(Profile(), noteTypes, notesByNoteType);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Sources of further information", pageModel.FurtherInformation.Heading);
        Assert.Equal("OIE Terrestrial Manual", Assert.Single(pageModel.FurtherInformation.Notes).NoteTextHtml);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToAnEmptyGroup_WhenTheNoteTypeIsNotFound()
    {
        var pageModel = CreatePageModel(Profile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.FurtherInformation.Notes);
        Assert.Equal("There are no sources of further information to display.", pageModel.FurtherInformation.EmptyMessage);
    }

    [Fact]
    public async Task OnGetAsync_StillReturnsThePage_WhenLoadingFurtherInformationFails()
    {
        var pageModel = CreatePageModel(Profile(), throwOnGetProfileNoteTypes: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.FurtherInformation.Notes);
    }
}

public class EditProfileReviewCommentsModelTests
{
    private static EditProfileReviewCommentsModel CreatePageModel(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileReviewCommentDto>? reviewComments = null,
        Exception? throwOnGetProfileSectionReviewComments = null) =>
        Configure(new EditProfileReviewCommentsModel(
            new FakeApiClient(manageProfile: manageProfile),
            new FakeProfileSectionsApiService(
                metadata: TwoSectionMetadata(),
                reviewComments: reviewComments,
                throwOnGetProfileSectionReviewComments: throwOnGetProfileSectionReviewComments),
            new AlwaysEnabledLogger<EditProfileReviewCommentsModel>()));

    [Fact]
    public void ActiveTab_IsReviewComments() =>
        Assert.Equal(ProfileSectionTab.ReviewComments, CreatePageModel(Profile()).ActiveTab);

    [Fact]
    public async Task OnGetAsync_LoadsReviewComments_IndentedByThreadDepth()
    {
        var reviewComments = new List<ProfileReviewCommentDto>
        {
            new()
            {
                CommentSubject = "Top level",
                UserFullName = "Jane Doe",
                Level = 1,
                CommentDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                CommentSubject = "A reply",
                UserFullName = "John Smith",
                Level = 2,
                CommentDate = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var pageModel = CreatePageModel(DraftProfile(), reviewComments);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.ReviewComments.Count);
        Assert.Equal("Top level", pageModel.ReviewComments[0].IndentedSubject);
        Assert.Equal("11/09/2026", pageModel.ReviewComments[0].CommentDate);
        Assert.StartsWith("\u00A0", pageModel.ReviewComments[1].IndentedSubject, StringComparison.Ordinal);
        Assert.EndsWith("A reply", pageModel.ReviewComments[1].IndentedSubject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheUserCannotViewReviewComments()
    {
        var reviewComments = new List<ProfileReviewCommentDto> { new() { CommentSubject = "Top level" } };

        var pageModel = CreatePageModel(Profile(), reviewComments);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(pageModel.ReviewComments);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotFail_WhenLoadingReviewCommentsThrows()
    {
        var pageModel = CreatePageModel(DraftProfile(), throwOnGetProfileSectionReviewComments: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.ReviewComments);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsThePage_WhenTheProfileHasNoVersion()
    {
        var pageModel = CreatePageModel(DraftProfile() with { CurrentProfileVersionId = Guid.Empty });

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasNoVersion);
    }
}

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
