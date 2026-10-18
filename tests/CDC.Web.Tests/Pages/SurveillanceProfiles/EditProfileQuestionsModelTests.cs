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
        ProfileVersionValidationStatusDto? validationStatus = null,
        ProfileSectionValidationResultDto? sectionValidationResult = null,
        ProfileSectionWriteResult? clearResult = null,
        Exception? throwOnClearProfileVersionSection = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new EditProfileQuestionsModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            new FakeProfileSectionsApiService(
                metadata: metadata,
                answers: answers,
                referenceValuesByTable: referenceValuesByTable,
                validationStatus: validationStatus,
                sectionValidationResult: sectionValidationResult,
                clearResult: clearResult,
                throwOnClearProfileVersionSection: throwOnClearProfileVersionSection),
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
        ProfileSectionWriteResult? saveResult = null,
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
    public async Task OnPostSaveSectionAsync_SendsParsedAnswers_AndReportsTheLegacySuccessMessage()
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
            saveResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Success, FieldsChangedCount = 4 });

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Profile section save succeeded", pageModel.ActionMessage);
        Assert.False(pageModel.ActionMessageIsError);
        Assert.Empty(pageModel.SaveValidationErrors);
        Assert.False(pageModel.HasError);
        Assert.False(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsTheTrimmedAnswersAndTheSectionsRowVersionItWasShownWith()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            new Dictionary<string, string>
            {
                [$"field_{BooleanFieldId}"] = "true",
                [$"field_{TextFieldId}"] = "  Some notes  "
            });

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, Convert.ToBase64String(rowVersion), CancellationToken.None);

        Assert.Equal(rowVersion, service.SavedLastUpdated);
        Assert.NotNull(service.SavedAnswers);
        Assert.True(service.SavedAnswers.Single(answer => answer.ProfileFieldId == BooleanFieldId).BooleanValue);
        Assert.Equal("Some notes", service.SavedAnswers.Single(answer => answer.ProfileFieldId == TextFieldId).TextValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base 64")]
    [InlineData("AQID")]
    public async Task OnPostSaveSectionAsync_SendsNoRowVersion_WhenThePostedOneIsMissingOrNotARowVersion(string? lastUpdated)
    {
        var (pageModel, service) = CreateValidatingPageModel(EditableProfile());

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, lastUpdated, CancellationToken.None);

        Assert.Equal(1, service.SaveProfileVersionSectionCallCount);
        Assert.Null(service.SavedLastUpdated);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsTheReasonInTheLegacyWording_AndKeepsThePageUsable_WhenTheSectionWasEditedByAnotherUser()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            saveResult: new ProfileSectionWriteResult
            {
                Outcome = ProfileSectionWriteOutcome.Conflict,
                ErrorMessage = "The profile version section has been edited by another user"
            });

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Profile section save failed: The profile version section has been edited by another user", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.True(pageModel.HasFormErrors);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Questions);
        Assert.Equal(0, service.ValidateProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsTheFailure_AndKeepsThePageUsable_WhenCdcApiThrows()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), throwOnSave: new HttpRequestException("boom"));

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.StartsWith("Profile section save failed: ", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Questions);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_DoesNotSave_WhenTheUserCannotEditTheSection()
    {
        var (pageModel, service) = CreateValidatingPageModel(Profile());

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
        Assert.Null(pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_RefreshesTheValidationIcons_AfterSaving()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            validationStatus: StandardSectionStatus(isValid: false, StandardQuestionId),
            validationStatusAfterWrite: StandardSectionStatus(isValid: true));

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.True(pageModel.GetSectionValidationStatus(StandardSectionId));
        Assert.True(Assert.Single(pageModel.Questions).IsValid);
        Assert.Equal(2, service.GetValidationStatusCallCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_DoesNotReadAFieldThatIsReadOnlyForTheKindOfProfile()
    {
        var metadata = StandardQuestionMetadata();
        var readOnlyField = metadata.Sections[0].Questions[0].Fields.Single(field => field.Id == TextFieldId) with { IsReadOnlyCurrentSituation = true };
        metadata = metadata with
        {
            Sections = [metadata.Sections[0] with { Questions = [metadata.Sections[0].Questions[0] with { Fields = [.. metadata.Sections[0].Questions[0].Fields.Where(field => field.Id != TextFieldId), readOnlyField] }] }]
        };
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            new Dictionary<string, string> { [$"field_{BooleanFieldId}"] = "true", [$"field_{TextFieldId}"] = "tampered" },
            metadata: metadata);

        await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.NotNull(service.SavedAnswers);
        Assert.DoesNotContain(service.SavedAnswers, answer => answer.ProfileFieldId == TextFieldId);
        Assert.Contains(service.SavedAnswers, answer => answer.ProfileFieldId == BooleanFieldId);
        Assert.False(Assert.Single(pageModel.Questions).Fields.Single(field => field.FieldId == TextFieldId).IsEditable);
        Assert.True(Assert.Single(pageModel.Questions).Fields.Single(field => field.FieldId == BooleanFieldId).IsEditable);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReadsTheFieldsThatAreReadOnlyOnlyForTheOtherKindOfProfile()
    {
        var metadata = StandardQuestionMetadata();
        var scenarioOnlyField = metadata.Sections[0].Questions[0].Fields.Single(field => field.Id == TextFieldId) with { IsReadOnlyScenario = true };
        metadata = metadata with
        {
            Sections = [metadata.Sections[0] with { Questions = [metadata.Sections[0].Questions[0] with { Fields = [.. metadata.Sections[0].Questions[0].Fields.Where(field => field.Id != TextFieldId), scenarioOnlyField] }] }]
        };
        var form = new Dictionary<string, string> { [$"field_{TextFieldId}"] = "Some notes" };
        var (currentSituation, currentService) = CreateValidatingPageModel(EditableProfile(), form, metadata: metadata);
        var (scenario, scenarioService) = CreateValidatingPageModel(EditableProfile() with { IsWhatIfScenario = true }, form, metadata: metadata);

        await currentSituation.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);
        await scenario.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Contains(currentService.SavedAnswers!, answer => answer.ProfileFieldId == TextFieldId);
        Assert.DoesNotContain(scenarioService.SavedAnswers!, answer => answer.ProfileFieldId == TextFieldId);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsAValidationError_ForAnInvalidDecimal_AndDoesNotSave()
    {
        var pageModel = CreatePageModelWithForm(
            new Dictionary<string, string> { [$"field_{DecimalFieldId}"] = "not-a-number" },
            throwOnSaveProfileVersionSection: new InvalidOperationException("Save should not have been called"));

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(pageModel.SaveValidationErrors, error => error.Contains("decimal", StringComparison.OrdinalIgnoreCase));
        Assert.Null(pageModel.ActionMessage);
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

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains(pageModel.SaveValidationErrors, error => error.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.Null(pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_TreatsABlankDecimalAndDate_AsClearingTheField()
    {
        var pageModel = CreatePageModelWithForm(new Dictionary<string, string>());

        var result = await pageModel.OnPostSaveSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Empty(pageModel.SaveValidationErrors);
        Assert.Equal(EditProfileQuestionsModel.SaveSucceededMessage, pageModel.ActionMessage);
    }

    private static ProfileVersionValidationStatusDto StandardSectionStatus(bool isValid, params Guid[] invalidQuestionIds) => new()
    {
        ProfileVersionId = ProfileVersionId,
        Sections = [new ProfileSectionValidationStatusDto { ProfileSectionId = StandardSectionId, IsValid = isValid, InvalidQuestionIds = invalidQuestionIds }]
    };

    private static ProfileSectionInvalidReasonDto QuestionReason(string description = "\"Is it endemic? - Endemic\" is required") => new()
    {
        QuestionId = StandardQuestionId,
        SectionNumber = 3,
        QuestionNumber = 1,
        QuestionShortName = "Q1",
        Description = description
    };

    private static (EditProfileQuestionsModel PageModel, FakeProfileSectionsApiService Service) CreateValidatingPageModel(
        ManageProfileViewModel profile,
        IDictionary<string, string>? formValues = null,
        ProfileVersionValidationStatusDto? validationStatus = null,
        ProfileVersionValidationStatusDto? validationStatusAfterWrite = null,
        ProfileSectionValidationResultDto? sectionValidationResult = null,
        Exception? throwOnSave = null,
        Exception? throwOnValidate = null,
        Exception? throwOnGetValidationStatus = null,
        ProfileSectionWriteResult? saveResult = null,
        ProfileSectionWriteResult? clearResult = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: metadata ?? StandardQuestionMetadata(),
            answers: answers,
            validationStatus: validationStatus,
            validationStatusAfterWrite: validationStatusAfterWrite,
            sectionValidationResult: sectionValidationResult,
            saveResult: saveResult,
            clearResult: clearResult,
            throwOnSaveProfileVersionSection: throwOnSave,
            throwOnValidateProfileVersionSection: throwOnValidate,
            throwOnGetProfileVersionValidationStatus: throwOnGetValidationStatus);

        var pageModel = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(new FakeApiClient(manageProfile: profile), service, new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            formValues ?? new Dictionary<string, string>(),
            StandardSectionId);

        return (pageModel, service);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_SavesTheAnswers_ThenListsTheInvalidReasons()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            saveResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Success, FieldsChangedCount = 4 },
            sectionValidationResult: new ProfileSectionValidationResultDto { IsValid = false, InvalidReasons = [QuestionReason(), QuestionReason("\"Notes\" is required")] });

        var result = await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(1, service.SaveProfileVersionSectionCallCount);
        Assert.Equal(1, service.ValidateProfileVersionSectionCallCount);
        Assert.Equal(EditProfileQuestionsModel.SaveSucceededMessage, pageModel.ActionMessage);
        Assert.True(pageModel.HasValidated);
        Assert.Equal(2, pageModel.InvalidReasons.Count);
        Assert.True(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_SendsTheSectionsRowVersionWithTheSave()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        var (pageModel, service) = CreateValidatingPageModel(EditableProfile());

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, Convert.ToBase64String(rowVersion), CancellationToken.None);

        Assert.Equal(rowVersion, service.SavedLastUpdated);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_ReportsNoErrors_WhenTheSectionIsValid()
    {
        var (pageModel, _) = CreateValidatingPageModel(
            EditableProfile(),
            sectionValidationResult: new ProfileSectionValidationResultDto { IsValid = true });

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.True(pageModel.HasValidated);
        Assert.Empty(pageModel.InvalidReasons);
        Assert.False(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_DoesNotSave_WhenTheUserCannotEditTheSection()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            Profile(),
            sectionValidationResult: new ProfileSectionValidationResultDto { IsValid = false, InvalidReasons = [QuestionReason()] });

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
        Assert.Equal(1, service.ValidateProfileVersionSectionCallCount);
        Assert.True(pageModel.HasValidated);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_DoesNotValidate_WhenTheAnswersAreInvalid()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            new Dictionary<string, string> { [$"field_{DecimalFieldId}"] = "not a number" });

        var result = await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Single(pageModel.SaveValidationErrors);
        Assert.False(pageModel.HasValidated);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
        Assert.Equal(0, service.ValidateProfileVersionSectionCallCount);
        Assert.True(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_DoesNotValidate_WhenSavingFails()
    {
        var (pageModel, service) = CreateValidatingPageModel(EditableProfile(), throwOnSave: new HttpRequestException("boom"));

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.True(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasValidated);
        Assert.Equal(0, service.ValidateProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_DoesNotValidate_WhenTheSectionWasEditedByAnotherUser()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            saveResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Conflict, ErrorMessage = "The profile version section has been edited by another user" });

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal("Profile section save failed: The profile version section has been edited by another user", pageModel.ActionMessage);
        Assert.False(pageModel.HasValidated);
        Assert.Equal(0, service.ValidateProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_SetsHasError_WhenValidatingFails()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), throwOnValidate: new HttpRequestException("boom"));

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.False(pageModel.HasValidated);
    }

    [Fact]
    public async Task OnPostValidateSectionAsync_RefreshesTheValidationIcons_AfterSaving()
    {
        var (pageModel, service) = CreateValidatingPageModel(
            EditableProfile(),
            validationStatus: StandardSectionStatus(isValid: false, StandardQuestionId),
            validationStatusAfterWrite: StandardSectionStatus(isValid: true));

        await pageModel.OnPostValidateSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.True(pageModel.GetSectionValidationStatus(StandardSectionId));
        Assert.True(Assert.Single(pageModel.Questions).IsValid);
        Assert.Equal(2, service.GetValidationStatusCallCount);
    }

    [Fact]
    public void BuildValidationReasonText_NumbersTheQuestion_AndStripsMarkupFromTheDescription()
    {
        var reason = QuestionReason("Cattle, \"<p>Is it <i>endemic</i>?</p> - Endemic\" is required");

        var text = EditProfileQuestionsModel.BuildValidationReasonText(reason);

        Assert.Equal("3.1 Cattle, \"Is it endemic? - Endemic\" is required", text);
    }

    [Fact]
    public void BuildValidationReasonUrl_LinksAQuestionError_ToTheQuestionsHeading()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile());

        var url = pageModel.BuildValidationReasonUrl(QuestionReason());

        Assert.Equal("#profile-questions-accordion-heading-3-1", url);
    }

    [Fact]
    public async Task BuildValidationReasonUrl_LinksARevisionDateError_ToTheAllRevisionDatesTab_WhenTheUserCanSeeIt()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile() with { CanViewAllRevisionDates = true });
        await pageModel.OnGetAsync(CancellationToken.None);
        var revisionReason = new ProfileSectionInvalidReasonDto { SectionNumber = 3, Description = "\"Revision dates, Policy review frequency\" is required" };

        var url = pageModel.BuildValidationReasonUrl(revisionReason);

        Assert.Equal($"/SurveillanceProfiles/EditProfileAllRevisionDates?profileId={ProfileTabTestData.ProfileId}&section={StandardSectionId}", url);
    }

    [Fact]
    public async Task BuildValidationReasonUrl_HasNoLinkForARevisionDateError_WhenTheUserCannotSeeTheTab()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile());
        await pageModel.OnGetAsync(CancellationToken.None);

        var url = pageModel.BuildValidationReasonUrl(new ProfileSectionInvalidReasonDto { SectionNumber = 3, Description = "\"Revision dates, VA review status\" is required" });

        Assert.Null(url);
    }

    private static readonly Guid ScientificPaperTypeId = Guid.NewGuid();
    private static readonly Guid LegislativeTypeId = Guid.NewGuid();
    private static readonly Guid FurtherInformationTypeId = Guid.NewGuid();
    private static readonly byte[] NoteRowVersion = [8, 7, 6, 5, 4, 3, 2, 1];

    private static ManageProfileViewModel ClearableProfile() => Profile() with { CanClearProfileSection = true };

    private static IReadOnlyList<ProfileNoteTypeDto> AllNoteTypes() =>
    [
        new ProfileNoteTypeDto { Id = ScientificPaperTypeId, Name = "Scientific paper reference" },
        new ProfileNoteTypeDto { Id = LegislativeTypeId, Name = "Legislative reference" },
        new ProfileNoteTypeDto { Id = FurtherInformationTypeId, Name = "Source of further information" }
    ];

    private static ProfileNoteDto NoteLinkedToTheSection() => new()
    {
        Id = Guid.NewGuid(),
        NoteText = "A note",
        LastUpdated = NoteRowVersion,
        QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = StandardSectionId, ProfileQuestionId = StandardQuestionId }]
    };

    private static Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> OneLinkedNoteOfEachType() => new()
    {
        [ScientificPaperTypeId] = [NoteLinkedToTheSection()],
        [LegislativeTypeId] = [NoteLinkedToTheSection()],
        [FurtherInformationTypeId] = [NoteLinkedToTheSection()]
    };

    private static (EditProfileQuestionsModel PageModel, FakeProfileSectionsApiService Service) CreateClearingPageModel(
        ManageProfileViewModel profile,
        ProfileSectionWriteResult? clearResult = null,
        Exception? throwOnClear = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notes = null,
        ProfileNoteChangeResult? noteChangeResult = null,
        Exception? throwOnGetNotes = null,
        ProfileSectionWriteResult? deleteCommentsResult = null,
        Exception? throwOnDeleteComments = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        ProfileVersionValidationStatusDto? validationStatus = null,
        ProfileVersionValidationStatusDto? validationStatusAfterWrite = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: StandardQuestionMetadata(),
            validationStatus: validationStatus,
            validationStatusAfterWrite: validationStatusAfterWrite,
            clearResult: clearResult,
            throwOnClearProfileVersionSection: throwOnClear,
            noteTypes: noteTypes ?? AllNoteTypes(),
            notesByNoteType: notes,
            throwOnGetProfileNotesBySection: throwOnGetNotes,
            noteChangeResult: noteChangeResult,
            deleteAllReviewCommentsResult: deleteCommentsResult,
            throwOnDeleteAllReviewComments: throwOnDeleteComments);

        var pageModel = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(new FakeApiClient(manageProfile: profile), service, new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            new Dictionary<string, string>(),
            StandardSectionId);

        return (pageModel, service);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_ClearsTheSection_ThenUnlinksEachKindOfReferenceFromItsQuestions_ThenDeletesTheReviewComments()
    {
        var (pageModel, service) = CreateClearingPageModel(ClearableProfile(), notes: OneLinkedNoteOfEachType());

        var result = await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Clear section succeeded", pageModel.ActionMessage);
        Assert.False(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasError);
        Assert.Equal(1, service.ClearProfileVersionSectionCallCount);
        Assert.Equal([ScientificPaperTypeId, LegislativeTypeId, FurtherInformationTypeId], service.NoteTypesChanged);
        Assert.Equal(1, service.DeleteAllReviewCommentsCallCount);

        var removal = Assert.Single(service.SavedNoteUpdates!);
        Assert.Equal(NoteRowVersion, removal.LastUpdated);
        Assert.Empty(removal.QuestionReferenceAdds);
        var removed = Assert.Single(removal.QuestionReferenceRemoves);
        Assert.Equal(StandardSectionId, removed.ProfileSectionId);
        Assert.Equal(StandardQuestionId, removed.ProfileQuestionId);
        Assert.Empty(service.SavedNoteInserts!);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_SendsTheRowVersionTheSectionWasShownWith()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        var (pageModel, service) = CreateClearingPageModel(ClearableProfile());

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, Convert.ToBase64String(rowVersion), CancellationToken.None);

        Assert.Equal(rowVersion, service.ClearedLastUpdated);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_LeavesNotesThatAreNotLinkedToTheSectionAlone_ButStillDeletesTheReviewComments()
    {
        var elsewhere = new ProfileNoteDto
        {
            Id = Guid.NewGuid(),
            LastUpdated = NoteRowVersion,
            QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = Guid.NewGuid(), ProfileQuestionId = Guid.NewGuid() }]
        };
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            notes: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [ScientificPaperTypeId] = [elsewhere], [LegislativeTypeId] = [] });

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal(0, service.UpdateProfileNotesCallCount);
        Assert.Equal(1, service.DeleteAllReviewCommentsCallCount);
        Assert.Equal("Clear section succeeded", pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_SkipsANoteTypeThatDoesNotExist()
    {
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            notes: OneLinkedNoteOfEachType(),
            noteTypes: [new ProfileNoteTypeDto { Id = LegislativeTypeId, Name = "Legislative reference" }]);

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal([LegislativeTypeId], service.NoteTypesChanged);
        Assert.Equal("Clear section succeeded", pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_RefreshesTheAnswersAndTheValidationIcons_AfterClearing()
    {
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            validationStatus: StandardSectionStatus(isValid: true),
            validationStatusAfterWrite: StandardSectionStatus(isValid: false, StandardQuestionId));

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.False(pageModel.GetSectionValidationStatus(StandardSectionId));
        Assert.False(Assert.Single(pageModel.Questions).IsValid);
        Assert.Equal(2, service.GetValidationStatusCallCount);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_DoesNothing_WhenTheUserCannotClearTheSection()
    {
        var (pageModel, service) = CreateClearingPageModel(EditableProfile());

        var result = await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, service.ClearProfileVersionSectionCallCount);
        Assert.Null(pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_ReportsTheReasonInTheLegacyWording_AndStops_WhenTheSectionWasEditedByAnotherUser()
    {
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            clearResult: new ProfileSectionWriteResult
            {
                Outcome = ProfileSectionWriteOutcome.Conflict,
                ErrorMessage = "The profile version section has been edited by another user"
            },
            notes: OneLinkedNoteOfEachType());

        var result = await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Clear section failed: The profile version section has been edited by another user", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.True(pageModel.HasFormErrors);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Questions);
        Assert.Equal(0, service.UpdateProfileNotesCallCount);
        Assert.Equal(0, service.DeleteAllReviewCommentsCallCount);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_ReportsTheFailure_AndKeepsThePageUsable_WhenCdcApiThrows()
    {
        var (pageModel, service) = CreateClearingPageModel(ClearableProfile(), throwOnClear: new HttpRequestException("boom"));

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.StartsWith("Clear section failed: ", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Questions);
        Assert.Equal(0, service.DeleteAllReviewCommentsCallCount);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_StopsAtTheFirstReferenceThatCannotBeUnlinked_LeavingTheSectionCleared()
    {
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            notes: OneLinkedNoteOfEachType(),
            noteChangeResult: new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Conflict, ErrorMessage = "The note has been edited by another user" });

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal(
            "Clear section failed while trying to clear the scientific paper references: The note has been edited by another user",
            pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.Equal(1, service.ClearProfileVersionSectionCallCount);
        Assert.Equal(1, service.UpdateProfileNotesCallCount);
        Assert.Equal(0, service.DeleteAllReviewCommentsCallCount);
    }

    [Fact]
    public async Task OnPostClearSectionAsync_ReportsTheReviewCommentsThatCouldNotBeDeleted()
    {
        var (pageModel, service) = CreateClearingPageModel(
            ClearableProfile(),
            deleteCommentsResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Error, ErrorMessage = "Not permitted" });

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.Equal("Clear section failed while trying to clear the review comments: Not permitted", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.Equal(1, service.DeleteAllReviewCommentsCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnPostClearSectionAsync_ReportsAFailure_WhenTheReferencesOrReviewCommentsCannotBeReached(bool failOnReviewComments)
    {
        var (pageModel, _) = CreateClearingPageModel(
            ClearableProfile(),
            throwOnGetNotes: failOnReviewComments ? null : new HttpRequestException("boom"),
            throwOnDeleteComments: failOnReviewComments ? new HttpRequestException("boom") : null);

        await pageModel.OnPostClearSectionAsync(ProfileVersionId, null, CancellationToken.None);

        Assert.StartsWith("Clear section failed: ", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasError);
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
    public async Task OnGetAsync_MarksQuestionValid_WhenTheSectionsValidationStatusDoesNotListIt()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), validationStatus: StandardSectionStatus(isValid: true));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(Assert.Single(pageModel.Questions).IsValid);
    }

    [Fact]
    public async Task OnGetAsync_MarksQuestionInvalid_WhenTheSectionsValidationStatusListsIt()
    {
        var (pageModel, _) = CreateValidatingPageModel(
            EditableProfile(),
            validationStatus: StandardSectionStatus(isValid: false, StandardQuestionId));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(Assert.Single(pageModel.Questions).IsValid);
    }

    [Fact]
    public async Task OnGetAsync_LeavesQuestionValidityUnknown_WhenTheValidationStatusCannotBeLoaded()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), throwOnGetValidationStatus: new HttpRequestException("boom"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.Null(Assert.Single(pageModel.Questions).IsValid);
        Assert.Null(pageModel.GetSectionValidationStatus(StandardSectionId));
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

    [Fact]
    public async Task OnGetAsync_ExposesTheSectionsRowVersion_ForTheFormToPostBack()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), answers: new ProfileSectionAnswersDto { LastUpdated = rowVersion });

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(Convert.ToBase64String(rowVersion), pageModel.SectionLastUpdated);
    }

    [Fact]
    public async Task OnGetAsync_ExposesNoRowVersion_WhenTheVersionHasNoRecordOfTheSection()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(string.Empty, pageModel.SectionLastUpdated);
    }

    [Fact]
    public async Task OnGetAsync_HidesTheQuestionsTheAnswersMakeIrrelevant()
    {
        var status = new ProfileVersionValidationStatusDto
        {
            ProfileVersionId = ProfileVersionId,
            Sections = [new ProfileSectionValidationStatusDto { ProfileSectionId = StandardSectionId, IsValid = true, IrrelevantQuestionIds = [StandardQuestionId] }]
        };
        var (hidden, _) = CreateValidatingPageModel(EditableProfile(), validationStatus: status);
        var (shown, _) = CreateValidatingPageModel(EditableProfile(), validationStatus: StandardSectionStatus(isValid: true));

        await hidden.OnGetAsync(CancellationToken.None);
        await shown.OnGetAsync(CancellationToken.None);

        Assert.True(Assert.Single(hidden.Questions).IsHidden);
        Assert.False(Assert.Single(shown.Questions).IsHidden);
    }

    [Fact]
    public async Task OnGetAsync_ShowsEveryQuestion_WhenTheValidationStatusCannotBeLoaded()
    {
        var (pageModel, _) = CreateValidatingPageModel(EditableProfile(), throwOnGetValidationStatus: new HttpRequestException("boom"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(Assert.Single(pageModel.Questions).IsHidden);
    }

    [Fact]
    public async Task OnGetAsync_RendersAFieldThatIsReadOnlyForTheKindOfProfileAsAStaticValue()
    {
        var metadata = StandardQuestionMetadata();
        var question = metadata.Sections[0].Questions[0];
        metadata = metadata with
        {
            Sections =
            [
                metadata.Sections[0] with
                {
                    Questions =
                    [
                        question with
                        {
                            Fields =
                            [
                                question.Fields.Single(field => field.Id == BooleanFieldId),
                                question.Fields.Single(field => field.Id == TextFieldId) with { IsReadOnlyCurrentSituation = true }
                            ]
                        }
                    ]
                }
            ]
        };
        var (currentSituation, _) = CreateValidatingPageModel(EditableProfile(), metadata: metadata);
        var (scenario, _) = CreateValidatingPageModel(EditableProfile() with { IsWhatIfScenario = true }, metadata: metadata);

        await currentSituation.OnGetAsync(CancellationToken.None);
        await scenario.OnGetAsync(CancellationToken.None);

        Assert.False(Assert.Single(currentSituation.Questions).Fields.Single(field => field.FieldId == TextFieldId).IsEditable);
        Assert.True(Assert.Single(scenario.Questions).Fields.Single(field => field.FieldId == TextFieldId).IsEditable);
        Assert.True(Assert.Single(currentSituation.Questions).Fields.Single(field => field.FieldId == BooleanFieldId).IsEditable);
    }

    private static (EditProfileQuestionsModel PageModel, FakeProfileSectionsApiService Service) CreateNavigatingPageModel(
        ManageProfileViewModel profile,
        IDictionary<string, string>? formValues = null,
        Exception? throwOnSave = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: StandardQuestionMetadata(),
            throwOnSaveProfileVersionSection: throwOnSave);

        var pageModel = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(new FakeApiClient(manageProfile: profile), service, new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            formValues ?? new Dictionary<string, string>(),
            StandardSectionId);

        return (pageModel, service);
    }

    [Theory]
    [InlineData("References", "/SurveillanceProfiles/EditProfileReferences")]
    [InlineData("FurtherInformation", "/SurveillanceProfiles/EditProfileFurtherInformation")]
    [InlineData("ReviewComments", "/SurveillanceProfiles/EditProfileReviewComments")]
    [InlineData("AllRevisionDates", "/SurveillanceProfiles/EditProfileAllRevisionDates")]
    [InlineData("Contributions", "/SurveillanceProfiles/EditProfileContributions")]
    [InlineData("Questions", "/SurveillanceProfiles/EditProfileQuestions")]
    public async Task OnPostNavigateTabAsync_SavesTheAnswers_ThenRedirectsToTheTabsPage(string tab, string expectedPage)
    {
        var (pageModel, service) = CreateNavigatingPageModel(EditableProfile() with
        {
            CanViewReviewComments = true,
            CanViewAllRevisionDates = true,
            CanViewContributions = true
        });

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, tab, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(expectedPage, redirect.PageName);
        Assert.Equal(ProfileTabTestData.ProfileId, redirect.RouteValues!["profileId"]);
        Assert.Equal(StandardSectionId, redirect.RouteValues["section"]);
        Assert.Equal(1, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostNavigateTabAsync_DoesNotSave_WhenTheUserCannotEditTheSection()
    {
        var (pageModel, service) = CreateNavigatingPageModel(Profile());

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, "References", null, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostNavigateTabAsync_StaysOnThePage_WhenTheAnswersAreInvalid()
    {
        var (pageModel, service) = CreateNavigatingPageModel(
            EditableProfile(),
            new Dictionary<string, string> { [$"field_{DecimalFieldId}"] = "not a number" });

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, "References", null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Single(pageModel.SaveValidationErrors);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostNavigateTabAsync_StaysOnThePage_WhenSavingFails()
    {
        var (pageModel, _) = CreateNavigatingPageModel(EditableProfile(), throwOnSave: new HttpRequestException("boom"));

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, "References", null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostNavigateTabAsync_StaysOnThePage_WhenTheSectionWasEditedByAnotherUser()
    {
        var service = new FakeProfileSectionsApiService(
            metadata: StandardQuestionMetadata(),
            saveResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Conflict, ErrorMessage = "The profile version section has been edited by another user" });
        var pageModel = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(new FakeApiClient(manageProfile: EditableProfile()), service, new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            new Dictionary<string, string>(),
            StandardSectionId);

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, "References", null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Profile section save failed: The profile version section has been edited by another user", pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostNavigateTabAsync_SendsTheSectionsRowVersionWithTheSave()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        var (pageModel, service) = CreateNavigatingPageModel(EditableProfile());

        await pageModel.OnPostNavigateTabAsync(ProfileVersionId, "References", Convert.ToBase64String(rowVersion), CancellationToken.None);

        Assert.Equal(rowVersion, service.SavedLastUpdated);
    }

    [Theory]
    [InlineData("ReviewComments")]
    [InlineData("AllRevisionDates")]
    [InlineData("Contributions")]
    public async Task OnPostNavigateTabAsync_StaysOnThePage_WhenTheTabIsNotVisibleToTheUser(string tab)
    {
        var (pageModel, service) = CreateNavigatingPageModel(EditableProfile());

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, tab, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Species")]
    [InlineData("99")]
    public async Task OnPostNavigateTabAsync_StaysOnThePage_ForAnUnknownTab(string? tab)
    {
        var (pageModel, service) = CreateNavigatingPageModel(EditableProfile());

        var result = await pageModel.OnPostNavigateTabAsync(ProfileVersionId, tab, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }
}
