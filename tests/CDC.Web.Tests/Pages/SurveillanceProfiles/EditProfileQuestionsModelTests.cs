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
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByNoteType = null,
        Exception? throwOnGetProfileNoteTypes = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new EditProfileQuestionsModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            new FakeProfileSectionsApiService(
                metadata: metadata,
                answers: answers,
                referenceValuesByTable: referenceValuesByTable,
                noteTypes: noteTypes,
                notesByNoteType: notesByNoteType,
                throwOnGetProfileNoteTypes: throwOnGetProfileNoteTypes),
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
                        IsPerSpecies = true,
                        IsRepeating = true,
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
    public async Task OnGetAsync_PopulatesProfileTitle_WithScenarioTitleInBrackets_ForAWhatIfScenario()
    {
        var scenario = Profile() with { ProfileTitle = "Swine Fever", ScenarioTitle = "African", IsWhatIfScenario = true };
        var pageModel = CreatePageModel(scenario, metadata: TwoSectionMetadata());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Swine Fever (African)", pageModel.ProfileTitle);
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
        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
        Assert.Empty(pageModel.LegislativeReferences.Notes);
        Assert.Empty(pageModel.FurtherInformation.Notes);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesReferencesAndFurtherInformation_MatchedByNoteTypeName_RegardlessOfCasingOrSpacing()
    {
        var scientificPaperReferenceTypeId = Guid.NewGuid();
        var legislativeReferenceTypeId = Guid.NewGuid();
        var furtherInformationTypeId = Guid.NewGuid();

        var noteTypes = new List<ProfileNoteTypeDto>
        {
            new() { Id = scientificPaperReferenceTypeId, Name = "scientific paper reference", PluralName = "Scientific paper references" },
            new() { Id = legislativeReferenceTypeId, Name = "Legislative Reference", PluralName = "Legislative references" },
            new() { Id = furtherInformationTypeId, Name = "SourceOfFurtherInformation", PluralName = "Sources of further information" }
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

        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            noteTypes: noteTypes,
            notesByNoteType: notesByNoteType);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Scientific paper references", pageModel.ScientificPaperReferences.Heading);
        Assert.Equal(2, pageModel.ScientificPaperReferences.Notes.Count);
        Assert.Equal("Aardvark et al. (2019)", pageModel.ScientificPaperReferences.Notes[0].NoteTextHtml);
        Assert.Equal("1.1", pageModel.ScientificPaperReferences.Notes[1].QuestionReferenceDisplay);
        Assert.Contains("<a href=", pageModel.ScientificPaperReferences.Notes[1].NoteTextHtml);

        Assert.Equal("Legislative references", pageModel.LegislativeReferences.Heading);
        Assert.Single(pageModel.LegislativeReferences.Notes);

        Assert.Empty(pageModel.FurtherInformation.Notes);
        Assert.Equal("Sources of further information", pageModel.FurtherInformation.Heading);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToEmptyGroups_WhenNoteTypesAreNotFound()
    {
        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
        Assert.Equal("There are no scientific paper references to display.", pageModel.ScientificPaperReferences.EmptyMessage);
        Assert.Empty(pageModel.LegislativeReferences.Notes);
        Assert.Empty(pageModel.FurtherInformation.Notes);
    }

    [Fact]
    public async Task OnGetAsync_StillReturnsQuestions_WhenLoadingReferencesFails()
    {
        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = SummarySectionId,
            QuestionNames = [new ProfileQuestionNameDto { Id = QuestionId, Name = "Is the disease endemic in GB?" }],
            FieldValues = []
        };

        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            answers: answers,
            throwOnGetProfileNoteTypes: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Questions);
        Assert.Empty(pageModel.ScientificPaperReferences.Notes);
        Assert.Empty(pageModel.LegislativeReferences.Notes);
        Assert.Empty(pageModel.FurtherInformation.Notes);
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
