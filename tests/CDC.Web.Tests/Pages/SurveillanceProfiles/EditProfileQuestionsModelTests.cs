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
using Microsoft.Extensions.Primitives;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileQuestionsModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid SummarySectionId = Guid.NewGuid();
    private static readonly Guid EpidemiologySectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid ReferenceTableId = Guid.NewGuid();
    private static readonly Guid StandardSectionId = Guid.NewGuid();
    private static readonly Guid StandardQuestionId = Guid.NewGuid();
    private static readonly Guid BooleanFieldId = Guid.NewGuid();
    private static readonly Guid DecimalFieldId = Guid.NewGuid();
    private static readonly Guid DateFieldId = Guid.NewGuid();
    private static readonly Guid TextFieldId = Guid.NewGuid();

    private static EditProfileQuestionsModel CreatePageModel(
        ManageProfileViewModel? manageProfile = null,
        Exception? throwOnGetManageProfile = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null,
        Guid? section = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByNoteType = null,
        Exception? throwOnGetProfileNoteTypes = null,
        ProfileVersionValidationStatusDto? validationStatus = null,
        ProfileSectionValidationResultDto? sectionValidationResult = null,
        ClearProfileSectionResultDto? clearResult = null,
        Exception? throwOnClearProfileVersionSection = null,
        IReadOnlyList<ProfileSectionContributionDto>? contributions = null,
        IReadOnlyList<ProfileReviewCommentDto>? reviewComments = null,
        ProfileVersionAllRevisionDatesDto? allRevisionDates = null)
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
                throwOnGetProfileNoteTypes: throwOnGetProfileNoteTypes,
                validationStatus: validationStatus,
                sectionValidationResult: sectionValidationResult,
                clearResult: clearResult,
                throwOnClearProfileVersionSection: throwOnClearProfileVersionSection,
                contributions: contributions,
                reviewComments: reviewComments,
                allRevisionDates: allRevisionDates),
            new AlwaysEnabledLogger<EditProfileQuestionsModel>())
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

    /// <summary>One section with a single standard (non-per-species, non-repeating) question
    /// covering the editable data types exercised by the Save tests.</summary>
    private static ProfileQuestionnaireMetadataDto StandardQuestionMetadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = StandardSectionId,
                Name = "Epidemiology",
                ShortName = "Epi",
                SectionNumber = 3,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = StandardQuestionId,
                        SectionId = StandardSectionId,
                        ShortName = "Q1",
                        QuestionNumber = 1,
                        IsPerSpecies = false,
                        IsRepeating = false,
                        Fields =
                        [
                            new ProfileFieldMetadataDto { Id = BooleanFieldId, QuestionId = StandardQuestionId, Name = "Is it endemic?", FieldNumber = 1, DataTypeName = "Boolean", IsMandatory = true },
                            new ProfileFieldMetadataDto { Id = DecimalFieldId, QuestionId = StandardQuestionId, Name = "Mortality rate", FieldNumber = 2, DataTypeName = "Decimal" },
                            new ProfileFieldMetadataDto { Id = DateFieldId, QuestionId = StandardQuestionId, Name = "Date of onset", FieldNumber = 3, DataTypeName = "Date" },
                            new ProfileFieldMetadataDto { Id = TextFieldId, QuestionId = StandardQuestionId, Name = "Notes", FieldNumber = 4, DataTypeName = "Text" }
                        ]
                    }
                ]
            }
        ]
    };

    private static ManageProfileViewModel EditableProfile() =>
        Profile() with { CanEditProfileSection = true };

    private static ManageProfileViewModel DraftProfileWithAllTabsVisible() =>
        Profile() with
        {
            CanEditProfileSection = true,
            CanClearProfileSection = true,
            CanPrintProfileSection = true,
            CanViewReviewComments = true,
            CanViewContributions = true,
            CanViewAllRevisionDates = true
        };

    private static EditProfileQuestionsModel CreatePageModelWithForm(
        IDictionary<string, string> formValues,
        SaveProfileSectionResultDto? saveResult = null,
        Exception? throwOnSaveProfileVersionSection = null)
    {
        var pageModel = new EditProfileQuestionsModel(
            new FakeApiClient(manageProfile: EditableProfile()),
            new FakeProfileSectionsApiService(
                metadata: StandardQuestionMetadata(),
                saveResult: saveResult,
                throwOnSaveProfileVersionSection: throwOnSaveProfileVersionSection),
            new AlwaysEnabledLogger<EditProfileQuestionsModel>())
        {
            ProfileId = ProfileId,
            Section = StandardSectionId,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { Request = { Form = new FormCollection(formValues.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value))) } },
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            MetadataProvider = new EmptyModelMetadataProvider()
        };

        return pageModel;
    }

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
    public async Task OnGetAsync_PopulatesVersionInfo_FromTheProfilesCurrentVersionDisplay()
    {
        var profile = Profile() with
        {
            CurrentVersionNumber = "13.0",
            CurrentVersionIsPublished = true,
            CurrentVersionValidFrom = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
        };
        var pageModel = CreatePageModel(profile, metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Version 13.0 (Published) Valid from 11/09/2026", pageModel.VersionInfo);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesCanPrintProfileSectionAndCanViewProfileReports_FromTheProfile()
    {
        var profile = Profile() with { CanPrintProfileSection = true, CanViewProfileReports = true };
        var pageModel = CreatePageModel(profile, metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanPrintProfileSection);
        Assert.True(pageModel.CanViewProfileReports);
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
    public async Task OnGetAsync_PopulatesSectionValidationStatus_ForEachSectionReturnedByCdcApi()
    {
        var validationStatus = new ProfileVersionValidationStatusDto
        {
            ProfileVersionId = ProfileVersionId,
            Sections = [new ProfileSectionValidationStatusDto { ProfileSectionId = SummarySectionId, IsValid = true }]
        };

        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata(), validationStatus: validationStatus);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.GetSectionValidationStatus(SummarySectionId));
        Assert.Null(pageModel.GetSectionValidationStatus(EpidemiologySectionId));
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

    [Fact]
    public async Task OnGetAsync_PopulatesCanEditProfileSectionAndCanClearProfileSection_FromTheProfile()
    {
        var profile = Profile() with { CanEditProfileSection = true, CanClearProfileSection = true };
        var pageModel = CreatePageModel(profile, metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanEditProfileSection);
        Assert.True(pageModel.CanClearProfileSection);
        Assert.Equal(ProfileVersionId, pageModel.CurrentProfileVersionId);
    }

    [Fact]
    public async Task OnGetAsync_MarksFieldsEditable_ForAStandardQuestion_WhenUserCanEditProfileSection()
    {
        var pageModel = CreatePageModel(EditableProfile(), metadata: StandardQuestionMetadata(), section: StandardSectionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        var fields = Assert.Single(pageModel.Questions).Fields;
        Assert.All(fields, field => Assert.True(field.IsEditable));
    }

    [Fact]
    public async Task OnGetAsync_MarksFieldsReadOnly_ForAPerSpeciesOrRepeatingQuestion_EvenWhenUserCanEditProfileSection()
    {
        var profile = EditableProfile();
        var pageModel = CreatePageModel(profile, metadata: TwoSectionMetadata(), section: SummarySectionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        var fields = Assert.Single(pageModel.Questions).Fields;
        Assert.All(fields, field => Assert.False(field.IsEditable));
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsParsedAnswers_AndPopulatesFieldsSavedCount()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string>
            {
                [$"field_{BooleanFieldId}"] = "true",
                [$"field_{DecimalFieldId}"] = "12.5",
                [$"field_{DateFieldId}_day"] = "11",
                [$"field_{DateFieldId}_month"] = "9",
                [$"field_{DateFieldId}_year"] = "2026",
                [$"field_{TextFieldId}"] = "  Some notes  "
            },
            saveResult: new SaveProfileSectionResultDto { FieldsSavedCount = 4 });

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(4, pageModel.FieldsSavedCount);
        Assert.Empty(pageModel.SaveValidationErrors);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsAValidationError_ForAnInvalidDecimal_AndDoesNotSave()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string> { [$"field_{DecimalFieldId}"] = "not-a-number" },
            throwOnSaveProfileVersionSection: new InvalidOperationException("Save should not have been called"));

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(pageModel.SaveValidationErrors, error => error.Contains("decimal", StringComparison.OrdinalIgnoreCase));
        Assert.Null(pageModel.FieldsSavedCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsAValidationError_ForAnInvalidDate_AndDoesNotSave()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string>
            {
                [$"field_{DateFieldId}_day"] = "31",
                [$"field_{DateFieldId}_month"] = "2",
                [$"field_{DateFieldId}_year"] = "2026"
            },
            throwOnSaveProfileVersionSection: new InvalidOperationException("Save should not have been called"));

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(pageModel.SaveValidationErrors, error => error.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.Null(pageModel.FieldsSavedCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_TreatsABlankDecimalAndDate_AsClearingTheField()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string>(),
            saveResult: new SaveProfileSectionResultDto { FieldsSavedCount = 4 });

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Empty(pageModel.SaveValidationErrors);
        Assert.Equal(4, pageModel.FieldsSavedCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SetsHasError_WhenCdcApiThrows()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string> { [$"field_{TextFieldId}"] = "Some notes" },
            throwOnSaveProfileVersionSection: new HttpRequestException("boom"));

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.FieldsSavedCount);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_PopulatesUnansweredQuestions_WhenTheSectionIsInvalid()
    {
        var sectionValidationResult = new ProfileSectionValidationResultDto
        {
            IsValid = false,
            UnansweredQuestions = ["1.1 Is the disease endemic in GB?"]
        };

        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            sectionValidationResult: sectionValidationResult);

        var result = await pageModel.OnPostValidateSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasValidated);
        Assert.Equal(["1.1 Is the disease endemic in GB?"], pageModel.UnansweredQuestions);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_ReportsNoUnansweredQuestions_WhenTheSectionIsValid()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            sectionValidationResult: new ProfileSectionValidationResultDto { IsValid = true });

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.True(pageModel.HasValidated);
        Assert.Empty(pageModel.UnansweredQuestions);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_PopulatesFieldsClearedCount_AndReloadsTheClearedAnswers()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            clearResult: new ClearProfileSectionResultDto { FieldsClearedCount = 3 });

        var result = await pageModel.OnPostClearSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(3, pageModel.FieldsClearedCount);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_SetsHasError_WhenCdcApiThrows()
    {
        var pageModel = CreatePageModel(
            Profile(),
            metadata: TwoSectionMetadata(),
            throwOnClearProfileVersionSection: new HttpRequestException("boom"));

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.FieldsClearedCount);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesDraftOnlyTabVisibilityFlags_FromTheProfile()
    {
        var pageModel = CreatePageModel(DraftProfileWithAllTabsVisible(), metadata: TwoSectionMetadata());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanViewReviewComments);
        Assert.True(pageModel.CanViewContributions);
        Assert.True(pageModel.CanViewAllRevisionDates);
    }

    [Fact]
    public async Task OnGetAsync_LoadsContributions_WhenCanViewContributionsIsTrue()
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

        var pageModel = CreatePageModel(DraftProfileWithAllTabsVisible(), metadata: TwoSectionMetadata(), contributions: contributions);

        await pageModel.OnGetAsync(CancellationToken.None);

        var contribution = Assert.Single(pageModel.Contributions);
        Assert.Equal("Jane Doe", contribution.FullName);
        Assert.Equal("jdoe", contribution.UserName);
        Assert.Equal("APHA", contribution.OrganisationName);
        Assert.Equal("11/09/2026", contribution.LastContributionDate);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotLoadContributions_WhenCanViewContributionsIsFalse()
    {
        var contributions = new List<ProfileSectionContributionDto> { new() { FullName = "Jane Doe" } };

        var pageModel = CreatePageModel(Profile(), metadata: TwoSectionMetadata(), contributions: contributions);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Contributions);
    }

    [Fact]
    public async Task OnGetAsync_LoadsReviewComments_IndentedByThreadDepth_WhenCanViewReviewCommentsIsTrue()
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

        var pageModel = CreatePageModel(DraftProfileWithAllTabsVisible(), metadata: TwoSectionMetadata(), reviewComments: reviewComments);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.ReviewComments.Count);
        Assert.Equal("Top level", pageModel.ReviewComments[0].IndentedSubject);
        Assert.StartsWith("\u00A0", pageModel.ReviewComments[1].IndentedSubject, StringComparison.Ordinal);
        Assert.EndsWith("A reply", pageModel.ReviewComments[1].IndentedSubject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnGetAsync_LoadsAllRevisionDates_WhenCanViewAllRevisionDatesIsTrue()
    {
        var allRevisionDates = new ProfileVersionAllRevisionDatesDto
        {
            ProfileVersionId = ProfileVersionId,
            Sections =
            [
                new ProfileSectionRevisionDatesDto
                {
                    ProfileSectionId = SummarySectionId,
                    SectionName = "Summary",
                    SectionNumber = 1,
                    TechnicalReviewFrequencyMonths = 12,
                    NextTechnicalReview = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PolicyReviewFrequencyMonths = null,
                    NextPolicyReview = null
                }
            ]
        };

        var pageModel = CreatePageModel(DraftProfileWithAllTabsVisible(), metadata: TwoSectionMetadata(), allRevisionDates: allRevisionDates);

        await pageModel.OnGetAsync(CancellationToken.None);

        var section = Assert.Single(pageModel.AllRevisionDates);
        Assert.Equal("1 Summary", section.SectionDisplay);
        Assert.Equal("12 months", section.TechnicalReviewFrequencyDisplay);
        Assert.Equal("01/01/2027", section.NextTechnicalReviewDisplay);
        Assert.Equal("Not set", section.PolicyReviewFrequencyDisplay);
        Assert.Equal("Not set", section.NextPolicyReviewDisplay);
    }

    [Fact]
    public async Task OnGetAsync_MarksQuestionValid_WhenItsMandatoryFieldsAreAnswered()
    {
        var answers = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileSectionId = StandardSectionId,
            FieldValues = [new ProfileFieldValueDto { QuestionId = StandardQuestionId, FieldNumber = 1, BooleanValue = true }]
        };

        var pageModel = CreatePageModel(EditableProfile(), metadata: StandardQuestionMetadata(), answers: answers, section: StandardSectionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(Assert.Single(pageModel.Questions).IsValid);
    }

    [Fact]
    public async Task OnGetAsync_MarksQuestionInvalid_WhenAMandatoryFieldIsUnanswered()
    {
        var pageModel = CreatePageModel(EditableProfile(), metadata: StandardQuestionMetadata(), section: StandardSectionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(Assert.Single(pageModel.Questions).IsValid);
    }

    [Fact]
    public async Task OnGetAsync_SetsCanEditReferences_ToMatchCanEditProfileSection()
    {
        var editablePageModel = CreatePageModel(EditableProfile(), metadata: StandardQuestionMetadata(), section: StandardSectionId);
        var readOnlyPageModel = CreatePageModel(Profile(), metadata: StandardQuestionMetadata(), section: StandardSectionId);

        await editablePageModel.OnGetAsync(CancellationToken.None);
        await readOnlyPageModel.OnGetAsync(CancellationToken.None);

        Assert.True(Assert.Single(editablePageModel.Questions).CanEditReferences);
        Assert.False(Assert.Single(readOnlyPageModel.Questions).CanEditReferences);
    }
}
