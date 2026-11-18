using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.Extensions.Primitives;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// A repeating question holds any number of rows of answers. The Questions tab lists the rows in a table, lets the user add, change and delete
// them in a panel, and saves them with the rest of the section, as the legacy ProfileRowListPanel did.
public class EditProfileQuestionsRepeatingTests
{
    internal static readonly Guid SectionId = Guid.NewGuid();
    internal static readonly Guid SurveillanceQuestionId = Guid.NewGuid();
    internal static readonly Guid NotesQuestionId = Guid.NewGuid();
    internal static readonly Guid MechanismFieldId = Guid.NewGuid();
    internal static readonly Guid ProjectFieldId = Guid.NewGuid();
    internal static readonly Guid PurposeFieldId = Guid.NewGuid();
    internal static readonly Guid ContractorsFieldId = Guid.NewGuid();
    internal static readonly Guid CostFieldId = Guid.NewGuid();
    internal static readonly Guid ReviewDateFieldId = Guid.NewGuid();
    internal static readonly Guid NotesFieldId = Guid.NewGuid();
    internal static readonly Guid MechanismTableId = Guid.NewGuid();
    internal static readonly Guid ContractorsTableId = Guid.NewGuid();
    internal static readonly Guid ClinicalId = Guid.NewGuid();
    internal static readonly Guid AbattoirId = Guid.NewGuid();
    internal static readonly Guid AgencyId = Guid.NewGuid();
    internal static readonly Guid UniversityId = Guid.NewGuid();
    internal static readonly Guid FirstRowId = Guid.NewGuid();
    internal static readonly Guid SecondRowId = Guid.NewGuid();

    internal static ProfileQuestionnaireMetadataDto Metadata(bool readOnlyCurrent = false) => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Current surveillance activities in the UK",
                ShortName = "Surveillance",
                SectionNumber = 12,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = SurveillanceQuestionId,
                        SectionId = SectionId,
                        ShortName = "Surveillance activities",
                        QuestionNumber = 1,
                        IsRepeating = true,
                        Fields =
                        [
                            new ProfileFieldMetadataDto { Id = MechanismFieldId, QuestionId = SurveillanceQuestionId, Name = "Surveillance mechanism", FieldNumber = 1, DataTypeName = "List", ReferenceTableId = MechanismTableId, IsMandatory = true, IncludeInSummary = true, IsReadOnlyCurrentSituation = readOnlyCurrent },
                            new ProfileFieldMetadataDto { Id = ProjectFieldId, QuestionId = SurveillanceQuestionId, Name = "Surveillance project name", FieldNumber = 2, DataTypeName = "Text", IncludeInSummary = true, IsReadOnlyCurrentSituation = readOnlyCurrent },
                            new ProfileFieldMetadataDto { Id = PurposeFieldId, QuestionId = SurveillanceQuestionId, Name = "Purpose and detail", FieldNumber = 3, DataTypeName = "Long Text", IsReadOnlyCurrentSituation = readOnlyCurrent },
                            new ProfileFieldMetadataDto { Id = ContractorsFieldId, QuestionId = SurveillanceQuestionId, Name = "Lead contractor", FieldNumber = 4, DataTypeName = "MultiValueList", ReferenceTableId = ContractorsTableId, IsReadOnlyCurrentSituation = readOnlyCurrent },
                            new ProfileFieldMetadataDto { Id = CostFieldId, QuestionId = SurveillanceQuestionId, Name = "Cost", FieldNumber = 5, DataTypeName = "Decimal", IsReadOnlyCurrentSituation = readOnlyCurrent },
                            new ProfileFieldMetadataDto { Id = ReviewDateFieldId, QuestionId = SurveillanceQuestionId, Name = "Review date", FieldNumber = 6, DataTypeName = "Date" }
                        ]
                    },
                    new ProfileQuestionMetadataDto
                    {
                        Id = NotesQuestionId,
                        SectionId = SectionId,
                        ShortName = "Notes",
                        QuestionNumber = 2,
                        Fields = [new ProfileFieldMetadataDto { Id = NotesFieldId, QuestionId = NotesQuestionId, Name = "Notes", FieldNumber = 1, DataTypeName = "Long Text" }]
                    }
                ]
            }
        ]
    };

    internal static Dictionary<Guid, IReadOnlyList<ReferenceValueDto>> ReferenceValues() => new()
    {
        [MechanismTableId] = [new ReferenceValueDto { Id = ClinicalId, Value = "Clinical" }, new ReferenceValueDto { Id = AbattoirId, Value = "Abattoir" }],
        [ContractorsTableId] = [new ReferenceValueDto { Id = AgencyId, Value = "Agency" }, new ReferenceValueDto { Id = UniversityId, Value = "University" }]
    };

    internal static ProfileFieldValueDto RowValue(Guid rowId, int fieldNumber, bool noRow = false, string? text = null, Guid? list = null, decimal? number = null) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = SurveillanceQuestionId,
        FieldNumber = fieldNumber,
        ProfileVersionQuestionRowId = noRow ? null : rowId,
        TextValue = text,
        ListValue = list,
        DecimalValue = number
    };

    // Two recorded rows, which CDC.Api lists in the order it happens to read them, not the order the question keeps them in.
    internal static ProfileSectionAnswersDto Answers(params ProfileFieldValueDto[] values) => new()
    {
        QuestionRows =
        [
            new ProfileQuestionRowDto { Id = SecondRowId, QuestionId = SurveillanceQuestionId, SequenceNumber = 1 },
            new ProfileQuestionRowDto { Id = FirstRowId, QuestionId = SurveillanceQuestionId, SequenceNumber = 0 }
        ],
        FieldValues = values
    };

    internal static ProfileSectionAnswersDto TwoRowsAnswers() => Answers(
        RowValue(FirstRowId, 1, list: ClinicalId),
        RowValue(FirstRowId, 2, text: "Bovine TB clinical surveillance"),
        RowValue(FirstRowId, 3, text: "<p>Looks for <em>clinical</em> signs</p>"),
        RowValue(FirstRowId, 4, list: AgencyId),
        RowValue(FirstRowId, 4, list: UniversityId),
        RowValue(FirstRowId, 5, number: 12.5m),
        RowValue(SecondRowId, 1, list: AbattoirId),
        RowValue(SecondRowId, 2, text: "Abattoir inspection"));

    private static (EditProfileQuestionsModel Page, FakeProfileSectionsApiService Service) CreatePage(
        ManageProfileViewModel? profile = null,
        IDictionary<string, StringValues>? form = null,
        ProfileQuestionnaireMetadataDto? metadata = null,
        ProfileSectionAnswersDto? answers = null)
    {
        var service = new FakeProfileSectionsApiService(
            metadata: metadata ?? Metadata(),
            answers: answers ?? TwoRowsAnswers(),
            referenceValuesByTable: ReferenceValues());
        var page = ProfileTabTestData.Configure(
            new EditProfileQuestionsModel(
                new FakeApiClient(manageProfile: profile ?? ProfileTabTestData.DraftProfile()),
                service,
                new AlwaysEnabledLogger<EditProfileQuestionsModel>()),
            null,
            SectionId);

        if (form is not null)
        {
            page.HttpContext.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, StringValues>(form));
        }

        return (page, service);
    }

    private static string RowKey(Guid rowId, Guid fieldId) => ProfileQuestionRowView.InputName(rowId, fieldId);

    private static string RowsKey => ProfileQuestionRowView.RowsInputName(SurveillanceQuestionId);

    // What the page posts for a question's rows: the question's own empty value, then the rows in the order they are in.
    private static StringValues Rows(params Guid[] rowIds) => new(["", .. rowIds.Select(id => id.ToString())]);

    [Fact]
    public async Task OnGetAsync_ListsTheRowsOfARepeatingQuestion_InTheOrderTheQuestionKeepsThem_WithTheirAnswers()
    {
        var (page, _) = CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        var rows = page.Questions[0].Rows;
        Assert.NotNull(rows);
        Assert.Equal([FirstRowId, SecondRowId], rows.Select(row => row.RowId));
        Assert.Equal(ClinicalId.ToString(), rows[0].AnswerTo(MechanismFieldId)?.FormValue);
        Assert.Equal("Clinical", rows[0].AnswerTo(MechanismFieldId)?.SummaryText);
        Assert.Equal("Bovine TB clinical surveillance", rows[0].AnswerTo(ProjectFieldId)?.SummaryText);
        Assert.Equal("<p>Looks for <em>clinical</em> signs</p>", rows[0].AnswerTo(PurposeFieldId)?.FormValue);
        Assert.Equal($"{AgencyId},{UniversityId}", rows[0].AnswerTo(ContractorsFieldId)?.FormValue);
        Assert.Equal("Agency, University", rows[0].AnswerTo(ContractorsFieldId)?.SummaryText);
        Assert.Equal("12.5", rows[0].AnswerTo(CostFieldId)?.FormValue);
        Assert.Equal("Abattoir", rows[1].AnswerTo(MechanismFieldId)?.SummaryText);
        Assert.Equal(string.Empty, rows[1].AnswerTo(PurposeFieldId)?.FormValue);
    }

    [Fact]
    public async Task OnGetAsync_KeepsEachRowsAnswersApart_AndLeavesTheAnswersOutsideRowsToTheirOwnQuestion()
    {
        var answers = TwoRowsAnswers() with
        {
            FieldValues = [.. TwoRowsAnswers().FieldValues, new ProfileFieldValueDto { Id = Guid.NewGuid(), QuestionId = NotesQuestionId, FieldNumber = 1, TextValue = "<p>A note</p>" }]
        };
        var (page, _) = CreatePage(answers: answers);

        await page.OnGetAsync(CancellationToken.None);

        var rows = page.Questions[0].Rows!;
        Assert.Equal("Abattoir inspection", rows[1].AnswerTo(ProjectFieldId)?.FormValue);
        Assert.Equal(string.Empty, rows[1].AnswerTo(CostFieldId)?.FormValue);
        Assert.Null(page.Questions[1].Rows);
        Assert.Null(page.Questions[1].SpeciesRows);
        Assert.Equal("<p>A note</p>", page.Questions[1].Fields[0].TextValue);
    }

    [Fact]
    public async Task OnGetAsync_GivesARepeatingQuestionNoRows_WhenNoneIsRecorded()
    {
        var (page, _) = CreatePage(answers: new ProfileSectionAnswersDto());

        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.Questions[0].Rows!);
    }

    [Fact]
    public async Task OnGetAsync_OffersTheUserTheEditPanel_WhenTheyCanEditTheSection_ExceptForAFieldThePanelHasNoControlFor()
    {
        var (page, _) = CreatePage();

        await page.OnGetAsync(CancellationToken.None);

        var question = page.Questions[0];
        Assert.True(question.CanEditRows);
        Assert.Equal(
            [true, true, true, true, true, false],
            question.Fields.Select(field => field.IsEditable));
        Assert.Equal([MechanismFieldId, ProjectFieldId], question.Fields.Where(field => field.IncludeInSummary).Select(field => field.FieldId));
    }

    [Fact]
    public async Task OnGetAsync_OffersOnlyAViewOfTheRows_WhenTheUserCannotEditTheSection()
    {
        var (page, _) = CreatePage(profile: ProfileTabTestData.ReadOnlyDraftProfile());

        await page.OnGetAsync(CancellationToken.None);

        Assert.False(page.Questions[0].CanEditRows);
        Assert.All(page.Questions[0].Fields, field => Assert.False(field.IsEditable));
        Assert.Equal(2, page.Questions[0].Rows!.Count);
    }

    [Fact]
    public async Task OnGetAsync_OffersOnlyAViewOfTheRows_WhenEveryFieldIsReadOnlyForTheKindOfProfile()
    {
        var (page, _) = CreatePage(metadata: Metadata(readOnlyCurrent: true));

        await page.OnGetAsync(CancellationToken.None);

        Assert.False(page.Questions[0].CanEditRows);
        Assert.All(page.Questions[0].Fields, field => Assert.False(field.IsEditable));
    }

    [Fact]
    public async Task OnGetAsync_GivesAFieldWithNoLabelAFallbackNameThatSaysWhichQuestionItAnswers()
    {
        var metadata = Metadata();
        var notes = metadata.Sections[0].Questions[1] with { Fields = [metadata.Sections[0].Questions[1].Fields[0] with { Name = string.Empty }] };
        var (page, _) = CreatePage(metadata: metadata with { Sections = [metadata.Sections[0] with { Questions = [metadata.Sections[0].Questions[0], notes] }] });

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("Answer to question 12.2", page.Questions[1].Fields[0].LabelFallback);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsEachRowPosted_InTheOrderPosted_WithItsAnswers()
    {
        var newRowId = Guid.NewGuid();
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues>
        {
            ["profileVersionId"] = ProfileTabTestData.ProfileVersionId.ToString(),
            [RowsKey] = Rows(SecondRowId, newRowId, FirstRowId),
            [RowKey(SecondRowId, MechanismFieldId)] = AbattoirId.ToString(),
            [RowKey(SecondRowId, ProjectFieldId)] = "  Abattoir inspection  ",
            [RowKey(newRowId, MechanismFieldId)] = ClinicalId.ToString(),
            [RowKey(newRowId, ProjectFieldId)] = "A new project",
            [RowKey(newRowId, PurposeFieldId)] = "<p>New purpose</p>",
            [RowKey(newRowId, ContractorsFieldId)] = $"{AgencyId},{UniversityId}",
            [RowKey(newRowId, CostFieldId)] = "1,250.5",
            [RowKey(FirstRowId, MechanismFieldId)] = string.Empty
        });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        var saved = Assert.Single(service.SavedRepeatingQuestions!);
        Assert.Equal(SurveillanceQuestionId, saved.QuestionId);
        Assert.Equal([SecondRowId, newRowId, FirstRowId], saved.Rows.Select(row => row.Id));

        var second = saved.Rows[0].Answers;
        Assert.Equal(AbattoirId, second.Single(answer => answer.ProfileFieldId == MechanismFieldId).ListValue);
        Assert.Equal("Abattoir inspection", second.Single(answer => answer.ProfileFieldId == ProjectFieldId).TextValue);
        Assert.All(second, answer => Assert.Null(answer.SpeciesId));

        var added = saved.Rows[1].Answers;
        Assert.Equal(ClinicalId, added.Single(answer => answer.ProfileFieldId == MechanismFieldId).ListValue);
        Assert.Equal("<p>New purpose</p>", added.Single(answer => answer.ProfileFieldId == PurposeFieldId).TextValue);
        Assert.Equal([AgencyId, UniversityId], added.Single(answer => answer.ProfileFieldId == ContractorsFieldId).MultiListValues);
        Assert.Equal(1250.5m, added.Single(answer => answer.ProfileFieldId == CostFieldId).DecimalValue);

        Assert.Null(saved.Rows[2].Answers.Single(answer => answer.ProfileFieldId == MechanismFieldId).ListValue);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReadsOnlyTheAnswersThePagePosted_AndNeverADateOrAFieldReadOnlyForTheProfile()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues>
        {
            [RowsKey] = Rows(FirstRowId),
            [RowKey(FirstRowId, ProjectFieldId)] = "Only the project",
            [RowKey(FirstRowId, ReviewDateFieldId)] = "2026-01-01"
        });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        var answer = Assert.Single(Assert.Single(Assert.Single(service.SavedRepeatingQuestions!).Rows).Answers);
        Assert.Equal(ProjectFieldId, answer.ProfileFieldId);
        Assert.Equal("Only the project", answer.TextValue);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsAQuestionWithNoRows_WhenTheUserDeletedThemAll()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues> { [RowsKey] = Rows() });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        var saved = Assert.Single(service.SavedRepeatingQuestions!);
        Assert.Equal(SurveillanceQuestionId, saved.QuestionId);
        Assert.Empty(saved.Rows);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_LeavesAQuestionAlone_WhenThePageDidNotPostItsListOfRows()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues> { [RowKey(FirstRowId, ProjectFieldId)] = "Posted without the list" });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        Assert.Empty(service.SavedRepeatingQuestions!);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SkipsAnIdentifierThatIsNotOneRowOfItsOwn()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues>
        {
            [RowsKey] = new StringValues(["", "not an identifier", Guid.Empty.ToString(), FirstRowId.ToString(), FirstRowId.ToString(), SecondRowId.ToString()])
        });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        Assert.Equal([FirstRowId, SecondRowId], Assert.Single(service.SavedRepeatingQuestions!).Rows.Select(row => row.Id));
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_ReportsAnInvalidDecimalInARow_NamingTheRow_AndSendsNothing()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues>
        {
            [RowsKey] = Rows(FirstRowId, SecondRowId),
            [RowKey(FirstRowId, CostFieldId)] = "12.5",
            [RowKey(SecondRowId, CostFieldId)] = "not a number"
        });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        Assert.Equal(["12.1 Cost (row 2): Enter a valid decimal number"], page.SaveValidationErrors);
        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostSaveSectionAsync_SendsNoRows_WhenTheUserCannotEditTheSection()
    {
        var (page, service) = CreatePage(
            profile: ProfileTabTestData.ReadOnlyDraftProfile(),
            form: new Dictionary<string, StringValues> { [RowsKey] = Rows() });

        await page.OnPostSaveSectionAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        Assert.Equal(0, service.SaveProfileVersionSectionCallCount);
    }

    [Fact]
    public async Task OnPostRefreshQuestionsAsync_SavesTheRowsToo_WithoutAnnouncingTheSave()
    {
        var (page, service) = CreatePage(form: new Dictionary<string, StringValues> { [RowsKey] = Rows(FirstRowId) });

        await page.OnPostRefreshQuestionsAsync(ProfileTabTestData.ProfileVersionId, string.Empty, CancellationToken.None);

        Assert.Equal([FirstRowId], Assert.Single(service.SavedRepeatingQuestions!).Rows.Select(row => row.Id));
        Assert.Null(page.ActionMessage);
    }
}
