using CDC.Common.Contracts;
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

public class EditProfileQuestionsModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid SummarySectionId = Guid.NewGuid();
    private static readonly Guid EpidemiologySectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid ReferenceTableId = Guid.NewGuid();

    private static EditProfileQuestionsModel CreatePageModel(
        ManageProfileViewModel? manageProfile = null,
        Exception? throwOnGetManageProfile = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null,
        Guid? section = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new EditProfileQuestionsModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            new FakeProfileSectionsApiService(
                metadata: metadata,
                answers: answers,
                referenceValuesByTable: referenceValuesByTable),
            NullLogger<EditProfileQuestionsModel>.Instance)
        {
            ProfileId = ProfileId,
            Section = section,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };
    }

    private static ManageProfileViewModel Profile(Guid? currentProfileVersionId = null) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Bovine tuberculosis",
        CurrentProfileVersionId = currentProfileVersionId ?? ProfileVersionId
    };

    private static ProfileQuestionnaireMetadataDto TwoSectionMetadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SummarySectionId,
                Name = "Summary",
                ShortName = "Summ",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SummarySectionId,
                        ShortName = "Overview",
                        QuestionNumber = 1,
                        Fields =
                        [
                            new ProfileFieldMetadataDto
                            {
                                Id = Guid.NewGuid(),
                                QuestionId = QuestionId,
                                Name = "Is it endemic?",
                                FieldNumber = 1,
                                DataTypeName = "Boolean"
                            }
                        ]
                    }
                ]
            },
            new ProfileSectionMetadataDto
            {
                Id = EpidemiologySectionId,
                Name = "Epidemiology",
                ShortName = "Epi",
                SectionNumber = 2,
                Questions = []
            }
        ]
    };

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(pageModel.ProfileTitle);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithHasError_WhenLoadingTheProfileFails()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesProfileTitle_AndDefaultsToTheFirstSection_WhenNoSectionIsRequested()
    {
        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Bovine tuberculosis", pageModel.ProfileTitle);
        Assert.False(pageModel.HasError);
        Assert.Equal(SummarySectionId, pageModel.CurrentSection!.Id);
        Assert.Null(pageModel.PreviousSection);
        Assert.Equal(EpidemiologySectionId, pageModel.NextSection!.Id);
    }

    [Fact]
    public async Task OnGetAsync_SelectsTheRequestedSection_AndComputesItsNeighbours()
    {
        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata(), section: EpidemiologySectionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(EpidemiologySectionId, pageModel.CurrentSection!.Id);
        Assert.Equal(SummarySectionId, pageModel.PreviousSection!.Id);
        Assert.Null(pageModel.NextSection);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasNoVersion_WhenTheProfileHasNoCurrentVersion()
    {
        var pageModel = CreatePageModel(Profile(currentProfileVersionId: Guid.Empty), metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.HasNoVersion);
        Assert.Empty(pageModel.Questions);
    }

    [Fact]
    public async Task OnGetAsync_BuildsQuestionViews_WithRealNamesAndRecordedFieldValues()
    {
        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = SummarySectionId,
            QuestionNames = [new ProfileQuestionNameDto { Id = QuestionId, Name = "Is the disease endemic in GB?" }],
            FieldValues = [new ProfileFieldValueDto { Id = Guid.NewGuid(), QuestionId = QuestionId, FieldNumber = 1, BooleanValue = true }]
        };

        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata(), answers: answers);

        await pageModel.OnGetAsync(CancellationToken.None);

        var question = Assert.Single(pageModel.Questions);
        Assert.Equal("1.1", question.Number);
        Assert.Equal("Is the disease endemic in GB?", question.Text);
        var field = Assert.Single(question.Fields);
        Assert.Equal("Is it endemic?", field.Label);
        Assert.Equal("Yes", field.ValueDisplay);
        Assert.Empty(field.Options);
    }

    [Fact]
    public async Task OnGetAsync_RendersAListField_AsACheckboxGroup_WithTheRecordedOptionChecked()
    {
        var fieldId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections =
            [
                new ProfileSectionMetadataDto
                {
                    Id = SummarySectionId,
                    Name = "Summary",
                    SectionNumber = 1,
                    Questions =
                    [
                        new ProfileQuestionMetadataDto
                        {
                            Id = QuestionId,
                            SectionId = SummarySectionId,
                            ShortName = "Species",
                            QuestionNumber = 1,
                            Fields =
                            [
                                new ProfileFieldMetadataDto
                                {
                                    Id = fieldId,
                                    QuestionId = QuestionId,
                                    Name = "Affected species",
                                    FieldNumber = 1,
                                    DataTypeName = "List",
                                    ReferenceTableId = ReferenceTableId
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = SummarySectionId,
            QuestionNames = [],
            FieldValues = [new ProfileFieldValueDto { Id = Guid.NewGuid(), QuestionId = QuestionId, FieldNumber = 1, ListValue = optionId }]
        };

        var referenceValues = new Dictionary<Guid, IReadOnlyList<ReferenceValueDto>>
        {
            [ReferenceTableId] =
            [
                new ReferenceValueDto { Id = optionId, Value = "Cattle" },
                new ReferenceValueDto { Id = Guid.NewGuid(), Value = "Sheep" }
            ]
        };

        var pageModel = CreatePageModel(Profile(), metadata: metadata, answers: answers, referenceValuesByTable: referenceValues);

        await pageModel.OnGetAsync(CancellationToken.None);

        var field = Assert.Single(Assert.Single(pageModel.Questions).Fields);
        Assert.Equal(2, field.Options.Count);
        Assert.True(field.Options.Single(option => option.Text == "Cattle").IsChecked);
        Assert.False(field.Options.Single(option => option.Text == "Sheep").IsChecked);
    }
}
