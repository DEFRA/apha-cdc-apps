using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// A per-species question is answered once for each species the profile version profiles. The Questions tab lists the species in a table,
// lets the user change a species' answers in a panel, and saves them against the species, as the legacy PerSpeciesListPanel did.
public class EditProfileQuestionsSpeciesTests
{
    internal static readonly Guid SectionId = Guid.NewGuid();
    internal static readonly Guid WelfareQuestionId = Guid.NewGuid();
    internal static readonly Guid ImpactQuestionId = Guid.NewGuid();
    internal static readonly Guid WelfareFieldId = Guid.NewGuid();
    internal static readonly Guid CommentsFieldId = Guid.NewGuid();
    internal static readonly Guid ImpactFieldId = Guid.NewGuid();
    internal static readonly Guid AreasFieldId = Guid.NewGuid();
    internal static readonly Guid RateFieldId = Guid.NewGuid();
    internal static readonly Guid FirstContactFieldId = Guid.NewGuid();
    internal static readonly Guid ImpactTableId = Guid.NewGuid();
    internal static readonly Guid AreasTableId = Guid.NewGuid();
    internal static readonly Guid MildId = Guid.NewGuid();
    internal static readonly Guid SevereId = Guid.NewGuid();
    internal static readonly Guid NorthId = Guid.NewGuid();
    internal static readonly Guid SouthId = Guid.NewGuid();
    internal static readonly Guid CattleId = Guid.NewGuid();
    internal static readonly Guid BadgerId = Guid.NewGuid();

    internal static ProfileFieldMetadataDto Welfare(bool readOnlyCurrent = false) => new()
    {
        Id = WelfareFieldId,
        QuestionId = WelfareQuestionId,
        Name = "Compromised welfare?",
        FieldNumber = 1,
        DataTypeName = "Boolean",
        IsMandatory = true,
        IncludeInSummary = true,
        AffectsRelevanceOf = ["5.2", "5.3"],
        IsReadOnlyCurrentSituation = readOnlyCurrent
    };

    internal static ProfileFieldMetadataDto Comments(bool readOnlyCurrent = false) => new()
    {
        Id = CommentsFieldId,
        QuestionId = WelfareQuestionId,
        Name = "Supporting comments",
        FieldNumber = 2,
        DataTypeName = "Long Text",
        IncludeInSummary = true,
        IsReadOnlyCurrentSituation = readOnlyCurrent
    };

    internal static ProfileQuestionnaireMetadataDto Metadata(bool readOnlyCurrent = false) => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Animal welfare",
                ShortName = "Welfare",
                SectionNumber = 5,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = WelfareQuestionId,
                        SectionId = SectionId,
                        ShortName = "Welfare Issue",
                        QuestionNumber = 1,
                        IsPerSpecies = true,
                        Fields = [Welfare(readOnlyCurrent), Comments(readOnlyCurrent)]
                    },
                    new ProfileQuestionMetadataDto
                    {
                        Id = ImpactQuestionId,
                        SectionId = SectionId,
                        ShortName = "Impact",
                        QuestionNumber = 2,
                        IsPerSpecies = true,
                        Fields =
                        [
                            new ProfileFieldMetadataDto { Id = ImpactFieldId, QuestionId = ImpactQuestionId, Name = "Impact", FieldNumber = 1, DataTypeName = "List", ReferenceTableId = ImpactTableId, IncludeInSummary = true },
                            new ProfileFieldMetadataDto { Id = AreasFieldId, QuestionId = ImpactQuestionId, Name = "Areas", FieldNumber = 2, DataTypeName = "MultiValueList", ReferenceTableId = AreasTableId },
                            new ProfileFieldMetadataDto { Id = RateFieldId, QuestionId = ImpactQuestionId, Name = "Rate", FieldNumber = 3, DataTypeName = "Decimal" },
                            new ProfileFieldMetadataDto { Id = FirstContactFieldId, QuestionId = ImpactQuestionId, Name = "First contact", FieldNumber = 4, DataTypeName = "Date" }
                        ]
                    }
                ]
            }
        ]
    };

    internal static Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> ReferenceValues() => new()
    {
        [ImpactTableId] = [new ReferenceValueDto { Id = MildId, Value = "Mild" }, new ReferenceValueDto { Id = SevereId, Value = "Severe" }],
        [AreasTableId] = [new ReferenceValueDto { Id = NorthId, Value = "North" }, new ReferenceValueDto { Id = SouthId, Value = "South" }]
    };

    internal static ProfileSectionAnswersDto Answers(params ProfileFieldValueDto[] values) => new()
    {
        ProfiledSpecies = [new ProfileSpeciesDto { Id = CattleId, Name = "Cattle" }, new ProfileSpeciesDto { Id = BadgerId, Name = "<em>Meles meles</em> (badger)" }],
        FieldValues = values
    };

    internal static ProfileFieldValueDto Value(Guid questionId, int fieldNumber, Guid speciesId, bool? flag = null, string? text = null, Guid? list = null, decimal? number = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = questionId,
        FieldNumber = fieldNumber,
        SpeciesId = speciesId,
        BooleanValue = flag,
        TextValue = text,
        ListValue = list,
        DecimalValue = number
    };

    private static (EditProfileQuestionsModel Page, FakeProfileSectionsApiService Service) CreatePage(
        ManageProfileViewModel? profile = null,
        IDictionary<string, string>? form = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null,
        ProfileVersionValidationStatusDto? validationStatus = null,
        ProfileSectionWriteResult? saveResult = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: metadata ?? Metadata(),
            answers: answers ?? Answers(),
            referenceValuesByTable: ReferenceValues(),
            validationStatus: validationStatus,
            saveResult: saveResult);
        var page = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(
                new FakeApiClient(manageProfile: profile ?? ProfileTabTestData.DraftProfile()),
                service,
                new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            form ?? new Dictionary<string, string>(),
            SectionId);

        return (page, service);
    }

    private static string Key(Guid speciesId, Guid fieldId) => ProfileSpeciesRowView.InputName(speciesId, fieldId);

    internal static ProfileVersionValidationStatusDto Status(params ProfileQuestionSpeciesDto[] irrelevantSpecies) => new()
    {
        ProfileVersionId = ProfileTabTestData.ProfileVersionId,
        Sections = [new ProfileSectionValidationStatusDto { ProfileSectionId = SectionId, IsValid = true, IrrelevantSpecies = irrelevantSpecies }]
    };

    [Fact]
    public async Task OnGetAsync_ListsEachProfiledSpeciesInTheOrderCdcApiReturnsThem_WithItsAnswersToTheQuestion()
    {
        var answers = Answers(
            Value(WelfareQuestionId, 1, CattleId, flag: true),
            Value(WelfareQuestionId, 2, CattleId, text: "<p>Cattle <em>often</em> suffer</p>"),
            Value(WelfareQuestionId, 1, BadgerId, flag: false));
        var (page, _) = CreatePage(answers: answers);

        await page.OnGetAsync(CancellationToken.None);

        var rows = page.Questions[0].SpeciesRows;
        Assert.NotNull(rows);
        Assert.Equal([CattleId, BadgerId], rows.Select(row => row.SpeciesId));
        Assert.Equal("Cattle", rows[0].Name);
        var cattleWelfare = rows[0].AnswerTo(WelfareFieldId);
        Assert.Equal("true", cattleWelfare?.FormValue);
        Assert.Equal("Yes", cattleWelfare?.SummaryText);
        var cattleComments = rows[0].AnswerTo(CommentsFieldId);
        Assert.Equal("<p>Cattle <em>often</em> suffer</p>", cattleComments?.FormValue);
        Assert.Equal("Cattle often suffer", cattleComments?.SummaryText);
        Assert.Equal("false", rows[1].AnswerTo(WelfareFieldId)?.FormValue);
        Assert.Equal("No", rows[1].AnswerTo(WelfareFieldId)?.SummaryText);
        Assert.Equal(string.Empty, rows[1].AnswerTo(CommentsFieldId)?.FormValue);
    }

    [Fact]
    public async Task OnGetAsync_KeepsEachSpeciesAnswersApart_WhenTheSameFieldIsAnsweredForSeveralSpecies()
    {
        var answers = Answers(
            Value(ImpactQuestionId, 1, CattleId, list: SevereId),
            Value(ImpactQuestionId, 1, BadgerId, list: MildId),
            Value(ImpactQuestionId, 2, CattleId, list: NorthId),
            Value(ImpactQuestionId, 2, CattleId, list: SouthId),
            Value(ImpactQuestionId, 3, BadgerId, number: 2.5m));
        var (page, _) = CreatePage(answers: answers);

        await page.OnGetAsync(CancellationToken.None);

        var rows = page.Questions[1].SpeciesRows!;
        Assert.Equal(SevereId.ToString(), rows[0].AnswerTo(ImpactFieldId)?.FormValue);
        Assert.Equal("Severe", rows[0].AnswerTo(ImpactFieldId)?.SummaryText);
        Assert.Equal(MildId.ToString(), rows[1].AnswerTo(ImpactFieldId)?.FormValue);
        Assert.Equal($"{NorthId},{SouthId}", rows[0].AnswerTo(AreasFieldId)?.FormValue);
        Assert.Equal("North, South", rows[0].AnswerTo(AreasFieldId)?.SummaryText);
        Assert.Equal(string.Empty, rows[1].AnswerTo(AreasFieldId)?.FormValue);
        Assert.Equal("2.5", rows[1].AnswerTo(RateFieldId)?.FormValue);
        Assert.Equal("2.50", rows[1].AnswerTo(RateFieldId)?.SummaryText);
    }

    [Fact]
    public async Task OnGetAsync_MarksTheSpeciesAQuestionIsIrrelevantFor_ButStillListsThem()
    {
        var (page, _) = CreatePage(validationStatus: Status(new ProfileQuestionSpeciesDto { QuestionId = ImpactQuestionId, SpeciesId = BadgerId }));

        await page.OnGetAsync(CancellationToken.None);

        Assert.All(page.Questions[0].SpeciesRows!, row => Assert.True(row.IsRelevant));
        var impactRows = page.Questions[1].SpeciesRows!;
        Assert.True(impactRows.Single(row => row.SpeciesId == CattleId).IsRelevant);
        Assert.False(impactRows.Single(row => row.SpeciesId == BadgerId).IsRelevant);
    }

    [Fact]
    public async Task OnGetAsync_GivesAPerSpeciesQuestionNoRows_WhenTheVersionProfilesNoSpecies()
    {
        var (page, _) = CreatePage(answers: new ProfileSectionAnswersDto());

        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.Questions[0].SpeciesRows!);
    }

    [Fact]
    public async Task OnGetAsync_GivesAStandardQuestionNoSpeciesRows()
    {
        var (page, _) = CreatePage(metadata: Metadata() with
        {
            Sections = [Metadata().Sections[0] with { Questions = [Metadata().Sections[0].Questions[0] with { IsPerSpecies = false }] }]
        });

        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.Questions[0].SpeciesRows);
    }

    [Fact]
    public async Task OnGetAsync_GivesARepeatingQuestionNoSpeciesRows_AndLeavesItsFieldsReadOnly()
    {
        var (page, _) = CreatePage(metadata: Metadata() with
        {
            Sections = [Metadata().Sections[0] with { Questions = [Metadata().Sections[0].Questions[0] with { IsRepeating = true }] }]
        });

        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.Questions[0].SpeciesRows);
        Assert.All(page.Questions[0].Fields, field => Assert.False(field.IsEditable));
    }

    [Fact]
    public async Task OnGetAsync_OffersTheUserTheEditPanel_WhenTheyCanEditTheSection()
    {
        var (page, _) = CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        Assert.All(page.Questions, question => Assert.True(question.CanEditSpecies));
        Assert.All(page.Questions[0].Fields, field => Assert.True(field.IsEditable));
    }

    [Fact]
    public async Task OnGetAsync_OffersOnlyAViewOfTheAnswers_WhenTheUserCannotEditTheSection()
    {
        var (page, _) = CreatePage(profile: ProfileTabTestData.ReadOnlyDraftProfile());

        await page.OnGetAsync(CancellationToken.None);

        Assert.All(page.Questions, question => Assert.False(question.CanEditSpecies));
        Assert.All(page.Questions.SelectMany(question => question.Fields), field => Assert.False(field.IsEditable));
    }

    [Fact]
    public async Task OnGetAsync_OffersOnlyAViewOfTheAnswers_WhenEveryFieldIsReadOnlyForTheKindOfProfile()
    {
        var (current, _) = CreatePage(metadata: Metadata(readOnlyCurrent: true));
        var (scenario, _) = CreatePage(profile: ProfileTabTestData.DraftProfile() with { IsWhatIfScenario = true }, metadata: Metadata(readOnlyCurrent: true));

        await current.OnGetAsync(CancellationToken.None);
        await scenario.OnGetAsync(CancellationToken.None);

        Assert.False(current.Questions[0].CanEditSpecies);
        Assert.All(current.Questions[0].Fields, field => Assert.False(field.IsEditable));
        Assert.True(scenario.Questions[0].CanEditSpecies);
    }

    [Fact]
    public async Task OnGetAsync_KeepsTheEditPanelOpenToTheFieldsThatCanBeChanged_WhenOnlySomeAreReadOnly()
    {
        var metadata = Metadata();
        var question = metadata.Sections[0].Questions[0] with { Fields = [Welfare(readOnlyCurrent: true), Comments()] };
        var (page, _) = CreatePage(metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [question, metadata.Sections[0].Questions[1]] }] });

        await page.OnGetAsync(CancellationToken.None);

        Assert.True(page.Questions[0].CanEditSpecies);
        Assert.False(page.Questions[0].Fields.Single(field => field.FieldId == WelfareFieldId).IsEditable);
        Assert.True(page.Questions[0].Fields.Single(field => field.FieldId == CommentsFieldId).IsEditable);
    }

    [Fact]
    public async Task OnGetAsync_LeavesADateFieldOfAPerSpeciesQuestionReadOnly_BecauseThePanelCannotEditADate()
    {
        var (page, _) = CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        var fields = page.Questions[1].Fields;
        Assert.False(fields.Single(field => field.FieldId == FirstContactFieldId).IsEditable);
        Assert.True(fields.Single(field => field.FieldId == RateFieldId).IsEditable);
    }

    [Fact]
    public async Task OnGetAsync_CarriesTheSummaryColumnsAndTheRelevanceWarningOfEachField()
    {
        var (page, _) = CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        var welfare = page.Questions[0].Fields.Single(field => field.FieldId == WelfareFieldId);
        Assert.True(welfare.IncludeInSummary);
        Assert.Equal(["5.2", "5.3"], welfare.AffectsRelevanceOf);
        Assert.Equal("5.2, 5.3", welfare.AffectsRelevanceText);
        var areas = page.Questions[1].Fields.Single(field => field.FieldId == AreasFieldId);
        Assert.False(areas.IncludeInSummary);
        Assert.Empty(areas.AffectsRelevanceOf);
        Assert.Equal(string.Empty, areas.AffectsRelevanceText);
    }

    [Fact]
    public async Task OnGetAsync_OffersEveryItemOfAListFieldInThePanel_WithNoneSelected()
    {
        var (page, _) = CreatePage(answers: Answers(Value(ImpactQuestionId, 1, CattleId, list: SevereId)));

        await page.OnGetAsync(CancellationToken.None);

        var impact = page.Questions[1].Fields.Single(field => field.FieldId == ImpactFieldId);
        Assert.Equal(["Mild", "Severe"], impact.Options.Select(option => option.Text));
        Assert.DoesNotContain(impact.Options, option => option.IsChecked);
    }

    [Fact]
    public async Task OnGetAsync_GivesAStandardQuestionsFieldItsRelevanceWarning()
    {
        var metadata = Metadata();
        var standard = metadata.Sections[0].Questions[0] with { IsPerSpecies = false };
        var (page, _) = CreatePage(metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [standard] }] });

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("5.2, 5.3", page.Questions[0].Fields.Single(field => field.FieldId == WelfareFieldId).AffectsRelevanceText);
    }

    [Fact]
    public void InputName_NamesTheSpeciesAndTheField()
    {
        Assert.Equal($"species_{CattleId}_field_{WelfareFieldId}", ProfileSpeciesRowView.InputName(CattleId, WelfareFieldId));
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsEachSpeciesAnswerPosted_AgainstItsSpecies()
    {
        var form = new Dictionary<string, string>
        {
            [Key(CattleId, WelfareFieldId)] = "true",
            [Key(BadgerId, WelfareFieldId)] = "false",
            [Key(CattleId, CommentsFieldId)] = "  <p>Notes</p>  ",
            [Key(CattleId, ImpactFieldId)] = SevereId.ToString(),
            [Key(BadgerId, ImpactFieldId)] = string.Empty,
            [Key(CattleId, AreasFieldId)] = $"{NorthId},{SouthId}",
            [Key(BadgerId, RateFieldId)] = "12.5"
        };
        var (page, service) = CreatePage(form: form);

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        var saved = service.SavedAnswers!;
        Assert.Equal(EditProfileQuestionsModel.SaveSucceededMessage, page.ActionMessage);
        Assert.True(saved.Single(answer => answer.ProfileFieldId == WelfareFieldId && answer.SpeciesId == CattleId).BooleanValue);
        Assert.False(saved.Single(answer => answer.ProfileFieldId == WelfareFieldId && answer.SpeciesId == BadgerId).BooleanValue);
        Assert.Equal("<p>Notes</p>", saved.Single(answer => answer.ProfileFieldId == CommentsFieldId && answer.SpeciesId == CattleId).TextValue);
        Assert.Equal(SevereId, saved.Single(answer => answer.ProfileFieldId == ImpactFieldId && answer.SpeciesId == CattleId).ListValue);
        Assert.Null(saved.Single(answer => answer.ProfileFieldId == ImpactFieldId && answer.SpeciesId == BadgerId).ListValue);
        Assert.Equal([NorthId, SouthId], saved.Single(answer => answer.ProfileFieldId == AreasFieldId && answer.SpeciesId == CattleId).MultiListValues);
        Assert.Equal(12.5m, saved.Single(answer => answer.ProfileFieldId == RateFieldId && answer.SpeciesId == BadgerId).DecimalValue);
        Assert.Equal(7, saved.Count);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsNoAnswerForASpeciesAnswerThePageDidNotPost()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, string> { [Key(CattleId, WelfareFieldId)] = "true" });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        var answer = Assert.Single(service.SavedAnswers!);
        Assert.Equal(WelfareFieldId, answer.ProfileFieldId);
        Assert.Equal(CattleId, answer.SpeciesId);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsAnEmptyAnswer_ForASpeciesAnswerPostedBlank()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, string>
        {
            [Key(CattleId, WelfareFieldId)] = string.Empty,
            [Key(CattleId, AreasFieldId)] = string.Empty,
            [Key(CattleId, RateFieldId)] = string.Empty
        });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        var saved = service.SavedAnswers!;
        Assert.Null(saved.Single(answer => answer.ProfileFieldId == WelfareFieldId).BooleanValue);
        Assert.Empty(saved.Single(answer => answer.ProfileFieldId == AreasFieldId).MultiListValues);
        Assert.Null(saved.Single(answer => answer.ProfileFieldId == RateFieldId).DecimalValue);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_DoesNotReadAnAnswerForASpeciesTheVersionDoesNotProfile_OrAFieldThatIsReadOnlyForTheKindOfProfile()
    {
        var metadata = Metadata();
        var question = metadata.Sections[0].Questions[0] with { Fields = [Welfare(readOnlyCurrent: true), Comments()] };
        var form = new Dictionary<string, string>
        {
            [Key(Guid.NewGuid(), CommentsFieldId)] = "someone else's species",
            [Key(CattleId, WelfareFieldId)] = "false",
            [Key(CattleId, FirstContactFieldId)] = "01/02/2026",
            [Key(CattleId, CommentsFieldId)] = "kept"
        };
        var (page, service) = CreatePage(form: form, metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [question, metadata.Sections[0].Questions[1]] }] });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        var answer = Assert.Single(service.SavedAnswers!);
        Assert.Equal(CommentsFieldId, answer.ProfileFieldId);
        Assert.Equal(CattleId, answer.SpeciesId);
        Assert.Equal("kept", answer.TextValue);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsAnInvalidDecimalForASpecies_NamingTheSpecies_AndDoesNotSave()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, string> { [Key(BadgerId, RateFieldId)] = "lots" });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        var error = Assert.Single(page.SaveValidationErrors);
        Assert.Equal("5.2 Rate (Meles meles (badger)): Enter a valid decimal number", error);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsNoSpeciesAnswer_WhenTheUserCannotEditTheSection()
    {
        var (page, service) = CreatePage(
            profile: ProfileTabTestData.ReadOnlyDraftProfile(),
            form: new Dictionary<string, string> { [Key(CattleId, WelfareFieldId)] = "true" });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    // The legacy hidden "Save" button saved the section as Save does and then blanked the message, so that a change that made questions
    // irrelevant showed them hidden without announcing a save the user had not asked for.
    [Fact]
    public async Task OnPostRefreshQuestionsAsync_SavesTheAnswersAsSaveDoes_ButSaysNothingOfIt()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, string> { [Key(CattleId, WelfareFieldId)] = "false" });

        var result = await page.OnPostRefreshQuestionsAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(1, service.SaveProfileVersionSectionCallCount);
        Assert.Equal(CattleId, Assert.Single(service.SavedAnswers!).SpeciesId);
        Assert.Null(page.ActionMessage);
        Assert.False(page.HasFormErrors);

        // The validation status is read again after the save, so that the questions the answers made irrelevant are hidden.
        Assert.Equal(2, service.GetValidationStatusCallCount);
    }

    [Fact]
    public async Task OnPostRefreshQuestionsAsync_ReportsAFailureToSave_InTheLegacyWording()
    {
        var (page, _) = CreatePage(
            form: new Dictionary<string, string> { [Key(CattleId, WelfareFieldId)] = "false" },
            saveResult: new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Conflict, ErrorMessage = "The profile version section has been edited by another user" });

        await page.OnPostRefreshQuestionsAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        Assert.Equal("Profile section save failed: The profile version section has been edited by another user", page.ActionMessage);
        Assert.True(page.ActionMessageIsError);
    }

    [Fact]
    public async Task OnPostRefreshQuestionsAsync_ReportsAnInvalidAnswer_AndDoesNotSave()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, string> { [Key(CattleId, RateFieldId)] = "lots" });

        await page.OnPostRefreshQuestionsAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        Assert.Single(page.SaveValidationErrors);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostRefreshQuestionsAsync_DoesNotSave_WhenTheUserCannotEditTheSection()
    {
        var (page, service) = CreatePage(profile: ProfileTabTestData.ReadOnlyDraftProfile());

        await page.OnPostRefreshQuestionsAsync(ProfileTabTestData.ProfileVersionId, null, CancellationToken.None);

        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
        Assert.Null(page.ActionMessage);
    }
}
