using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.EditProfileQuestionsRepeatingTests;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Integration;

// Renders a repeating question of the Questions tab through the real Razor pipeline: the table of its rows, the panel that adds and edits a
// row, and the form the rows are posted back in.
public partial class EditProfileQuestionsRepeatingPageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}";
    private static readonly string BlankRowId = Guid.Empty.ToString();

    private readonly CdcWebTestFactory _factory;

    public EditProfileQuestionsRepeatingPageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    [GeneratedRegex("data-row=\"([0-9a-f-]+)\"")]
    private static partial Regex RowIdRegex();

    [GeneratedRegex("<(select|input|textarea)[^>]* name=")]
    private static partial Regex NamedControlRegex();

    private static FakeProfileSectionsApiService CreateApi(ProfileSectionAnswersDto? answers = null) => new(
        metadata: Metadata(),
        answers: answers ?? TwoRowsAnswers(),
        referenceValuesByTable: ReferenceValues());

    private async Task WithPageAsync(ManageProfileViewModel profile, FakeProfileSectionsApiService sections, Func<HttpClient, Task> test)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(sections);
                services.RemoveAll<IProfileQuestionsApiService>();
                services.AddSingleton<IProfileQuestionsApiService>(new FakeProfileQuestionsApiService());
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        await test(client);
    }

    private async Task<string> GetPageAsync(ManageProfileViewModel profile, FakeProfileSectionsApiService? sections = null)
    {
        var body = string.Empty;

        await WithPageAsync(profile, sections ?? CreateApi(), async client => body = await client.GetStringAsync(PagePath));

        return body;
    }

    // The part of the page of the repeating question, from its table to the end of its content.
    private static string QuestionMarkup(string body)
    {
        var start = body.IndexOf($"id=\"rows-{SurveillanceQuestionId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The question should have a table of rows.");

        var end = body.IndexOf("govuk-accordion__section\"", start, StringComparison.Ordinal);

        return body[start..(end < 0 ? body.Length : end)];
    }

    private static string TableBody(string question)
    {
        var start = question.IndexOf("<tbody", StringComparison.Ordinal);
        var end = question.IndexOf("</tbody>", StringComparison.Ordinal);

        return question[start..end];
    }

    [Fact]
    public async Task Table_HasAColumnForEachFieldTheSummaryIncludes_AndForTheEditAndDeleteLinks()
    {
        var body = await GetPageAsync(DraftProfile());

        var question = QuestionMarkup(body);
        var header = question[..question.IndexOf("</thead>", StringComparison.Ordinal)];

        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Surveillance mechanism</th>", header);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Surveillance project name</th>", header);
        Assert.DoesNotContain("Purpose and detail</th>", header);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\"><span class=\"govuk-visually-hidden\">Edit</span></th>", header);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\"><span class=\"govuk-visually-hidden\">Delete</span></th>", header);
        Assert.Contains("Answers to question 12.1", question);
    }

    [Fact]
    public async Task Table_HasARowForEachRecordedRow_InTheOrderTheQuestionKeepsThem_ShowingItsSummary()
    {
        var body = await GetPageAsync(DraftProfile());

        var rows = TableBody(QuestionMarkup(body));

        Assert.Equal([FirstRowId.ToString(), SecondRowId.ToString()], RowIdRegex().Matches(rows).Select(match => match.Groups[1].Value));
        Assert.Contains($"<td class=\"govuk-table__cell\" data-row-summary=\"{MechanismFieldId}\">Clinical</td>", rows);
        Assert.Contains($"<td class=\"govuk-table__cell\" data-row-summary=\"{ProjectFieldId}\">Bovine TB clinical surveillance</td>", rows);
        Assert.Contains($"<td class=\"govuk-table__cell\" data-row-summary=\"{MechanismFieldId}\">Abattoir</td>", rows);
    }

    [Fact]
    public async Task Table_OffersAnEditAndADeleteLinkForEachRow_ThatNameTheRowAndTheQuestion()
    {
        var body = await GetPageAsync(DraftProfile());

        var rows = TableBody(QuestionMarkup(body));
        var panelId = $"rows-{SurveillanceQuestionId}-panel";

        Assert.Contains(
            $"<button type=\"button\" class=\"govuk-link app-link-button\" aria-controls=\"{panelId}\" aria-expanded=\"false\" data-row-edit>Edit<span class=\"govuk-visually-hidden\"> answer <span data-row-number>1</span> to question 12.1</span></button>",
            rows);
        Assert.Contains("<span data-row-number>2</span> to question 12.1</span></button>", rows);
        Assert.Contains(
            "<button type=\"button\" class=\"govuk-link app-link-button\" data-row-delete>Delete<span class=\"govuk-visually-hidden\"> answer <span data-row-number>1</span> to question 12.1</span></button>",
            rows);
    }

    [Fact]
    public async Task Table_KeepsEachRowsAnswersInHiddenInputs_NamedForTheRowAndTheField_AndListsTheRowsInOrder()
    {
        var body = await GetPageAsync(DraftProfile());

        var question = QuestionMarkup(body);
        var rows = TableBody(question);

        Assert.Contains($"<input type=\"hidden\" name=\"row_{FirstRowId}_field_{MechanismFieldId}\" value=\"{ClinicalId}\" data-row-value=\"{MechanismFieldId}\" />", rows);
        Assert.Contains($"<input type=\"hidden\" name=\"row_{FirstRowId}_field_{ContractorsFieldId}\" value=\"{AgencyId},{UniversityId}\" data-row-value=\"{ContractorsFieldId}\" />", rows);
        Assert.Contains($"name=\"row_{SecondRowId}_field_{ProjectFieldId}\" value=\"Abattoir inspection\"", rows);

        // The question's own empty value comes first, then each row once, in order.
        var listed = Regex.Matches(question, $"name=\"rows_{SurveillanceQuestionId}\" value=\"([^\"]*)\"").Select(match => match.Groups[1].Value);
        Assert.Equal(["", FirstRowId.ToString(), SecondRowId.ToString(), BlankRowId], listed);
    }

    [Fact]
    public async Task Table_NamesNoHiddenInput_ForAFieldTheUserMayNotChange()
    {
        var body = await GetPageAsync(DraftProfile());

        var rows = TableBody(QuestionMarkup(body));

        Assert.Contains($"<input type=\"hidden\" value=\"\" data-row-value=\"{ReviewDateFieldId}\" />", rows);
        Assert.DoesNotContain($"_field_{ReviewDateFieldId}\"", rows);
    }

    [Fact]
    public async Task Table_SaysThereAreNoAnswers_AndHidesTheTable_WhenNoRowIsRecorded()
    {
        var body = await GetPageAsync(DraftProfile(), CreateApi(new ProfileSectionAnswersDto()));

        var question = QuestionMarkup(body);

        Assert.Contains("<p class=\"govuk-body\" data-row-empty>There are no answers to display.</p>", question);
        Assert.Contains("<table class=\"govuk-table app-row-table\" data-row-table hidden=\"hidden\">", question);
        Assert.DoesNotContain("data-row=\"" + FirstRowId, question);
    }

    [Fact]
    public async Task Table_HidesTheMessageThatThereAreNoAnswers_WhenARowIsRecorded()
    {
        var body = await GetPageAsync(DraftProfile());

        var question = QuestionMarkup(body);

        Assert.Contains("<p class=\"govuk-body\" data-row-empty hidden=\"hidden\">There are no answers to display.</p>", question);
        Assert.Contains("<table class=\"govuk-table app-row-table\" data-row-table>", question);
    }

    [Fact]
    public async Task Add_IsOffered_WithABlankRowForTheScriptToCopy_ThatNamesItsInputsForTheEmptyIdentifier()
    {
        var body = await GetPageAsync(DraftProfile());

        var question = QuestionMarkup(body);
        var template = question[question.IndexOf("<template data-row-template>", StringComparison.Ordinal)..];

        Assert.Contains("data-row-add>Add</button>", question);
        Assert.Contains($"<tr class=\"govuk-table__row\" data-row=\"{BlankRowId}\">", template);
        Assert.Contains($"name=\"row_{BlankRowId}_field_{MechanismFieldId}\" value=\"\"", template);
        Assert.Contains($"name=\"rows_{SurveillanceQuestionId}\" value=\"{BlankRowId}\"", template);
    }

    [Fact]
    public async Task Rows_AreOfferedToAUserWhoCannotEditTheSection_OnlyForViewing()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        var question = QuestionMarkup(body);

        Assert.Contains("data-row-edit>View<span class=\"govuk-visually-hidden\">", question);
        Assert.DoesNotContain(">Edit<span", question);
        Assert.DoesNotContain("data-row-add", question);
        Assert.DoesNotContain("data-row-delete", question);
        Assert.DoesNotContain("data-row-template", question);
        Assert.DoesNotContain($"name=\"rows_{SurveillanceQuestionId}\"", question);
        Assert.DoesNotMatch("<input type=\"hidden\" name=\"row_", question);
        Assert.Contains("data-row-can-edit=\"false\"", question);
        Assert.Contains("<h3 class=\"govuk-fieldset__heading\">View answer</h3>", question);
        Assert.Contains("data-row-ok>Close</button>", question);
        Assert.Matches("<div class=\"govuk-button-group app-button-group--right\">\\s*<button[^>]* data-row-ok>Close</button>", question);
        Assert.DoesNotContain("data-row-cancel", question);
    }

    [Fact]
    public async Task Panel_EditsOneRowAtATime_WithAControlForEachField_ThatIsNeverPosted()
    {
        var body = await GetPageAsync(DraftProfile());

        var question = QuestionMarkup(body);
        var panel = question[question.IndexOf("data-row-panel", StringComparison.Ordinal)..question.IndexOf("<template", StringComparison.Ordinal)];
        var panelId = $"rows-{SurveillanceQuestionId}-field-";

        Assert.Contains("<h3 class=\"govuk-fieldset__heading\">Edit answer</h3>", panel);
        Assert.Contains($"<label class=\"govuk-label govuk-label--s\" for=\"{panelId}{MechanismFieldId}\">Surveillance mechanism</label>", panel);
        Assert.Contains("<option value=\"\">Please select...</option>", panel);
        Assert.Contains($"<option value=\"{ClinicalId}\">Clinical</option>", panel);
        Assert.Contains($"id=\"{panelId}{ProjectFieldId}\" type=\"text\"", panel);
        Assert.Contains($"<textarea class=\"govuk-textarea app-richtext-editor\" id=\"{panelId}{PurposeFieldId}\"", panel);
        Assert.Contains($"id=\"{panelId}{ContractorsFieldId}-0\" type=\"checkbox\" value=\"{AgencyId}\"", panel);
        Assert.Contains($"id=\"{panelId}{CostFieldId}\" type=\"text\" inputmode=\"decimal\"", panel);
        Assert.Matches($"id=\"{panelId}{ReviewDateFieldId}\" type=\"text\"[^>]* disabled=\"disabled\"", panel);
        Assert.Contains("data-row-ok>OK</button>", panel);
        Assert.Contains("data-row-cancel>Cancel</button>", panel);
        Assert.Matches("<div class=\"govuk-button-group app-button-group--right\">\\s*<button[^>]* data-row-ok>OK</button>\\s*<button[^>]* data-row-cancel>Cancel</button>", panel);
        Assert.False(NamedControlRegex().IsMatch(panel), "No control of the panel should have a name, for none is posted.");
    }

    [Fact]
    public async Task Page_LoadsTheScriptsThatEditTheRows_AfterTheOnesTheySharedCodeWith()
    {
        var body = await GetPageAsync(DraftProfile());

        var richText = body.IndexOf("/js/richtext-toolbar.js", StringComparison.Ordinal);
        var shared = body.IndexOf("/js/answer-fields.js", StringComparison.Ordinal);
        var species = body.IndexOf("/js/question-species.js", StringComparison.Ordinal);
        var rows = body.IndexOf("/js/question-rows.js", StringComparison.Ordinal);

        Assert.True(richText >= 0 && shared > richText && species > shared && rows > shared);
    }

    [Fact]
    public async Task Save_SendsTheRowsPostedWithTheForm_AgainstTheQuestion()
    {
        var sections = CreateApi();
        var newRowId = Guid.NewGuid();

        await WithPageAsync(DraftProfile(), sections, async client =>
        {
            var page = await client.GetStringAsync(PagePath);
            var token = AntiforgeryTokenRegex().Match(page).Groups[1].Value;
            List<KeyValuePair<string, string>> form =
            [
                new("__RequestVerificationToken", token),
                new("profileVersionId", ProfileVersionId.ToString()),
                new("Section", SectionId.ToString()),
                new("lastUpdated", string.Empty),
                new($"rows_{SurveillanceQuestionId}", string.Empty),
                new($"rows_{SurveillanceQuestionId}", FirstRowId.ToString()),
                new($"rows_{SurveillanceQuestionId}", newRowId.ToString()),
                new($"row_{FirstRowId}_field_{ProjectFieldId}", "Changed project"),
                new($"row_{newRowId}_field_{ProjectFieldId}", "Added project")
            ];

            using var response = await client.PostAsync($"{PagePath}?handler=SaveSection", new FormUrlEncodedContent(form));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        var saved = Assert.Single(sections.SavedRepeatingQuestions!);
        Assert.Equal(SurveillanceQuestionId, saved.QuestionId);
        Assert.Equal([FirstRowId, newRowId], saved.Rows.Select(row => row.Id));
        Assert.Equal("Added project", Assert.Single(saved.Rows[1].Answers).TextValue);
    }
}
