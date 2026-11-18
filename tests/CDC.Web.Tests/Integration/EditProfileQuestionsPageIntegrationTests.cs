using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Integration;

// Renders the Questions tab of a profile section through the real Razor pipeline, as the legacy EditProfileQuestions.aspx laid it out.
public partial class EditProfileQuestionsPageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}";
    private static readonly string ReferencesPath = $"/SurveillanceProfiles/QuestionReferences/{ProfileId}";
    private const string HelpPath = "/SurveillanceProfiles/ProfileQuestionHelp";

    private static readonly Guid NoteTypeId = Guid.NewGuid();
    private static readonly Guid NoteId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly CdcWebTestFactory _factory;

    public EditProfileQuestionsPageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    // The version holds one reference of the one type, applied to the Summary section's only question.
    private static FakeProfileSectionsApiService CreateApi(Exception? throwOnGetNoteTypes = null) => new(
        metadata: TwoSectionMetadata(),
        noteTypes: [new ProfileNoteTypeDto { Id = NoteTypeId, Name = "Scientific paper reference", PluralName = "Scientific paper references" }],
        throwOnGetProfileNoteTypes: throwOnGetNoteTypes,
        notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
        {
            [NoteTypeId] =
            [
                new ProfileNoteDto
                {
                    Id = NoteId,
                    NoteText = "<p>Smith 2020 <em>Bovine</em></p>",
                    LastUpdated = RowVersion,
                    QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = SummarySectionId, ProfileQuestionId = QuestionId }]
                }
            ]
        });

    private async Task WithPageAsync(
        ManageProfileViewModel profile,
        Func<HttpClient, Task> test,
        FakeProfileQuestionsApiService? questions = null,
        FakeProfileSectionsApiService? sections = null)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(sections ?? CreateApi());
                services.RemoveAll<IProfileQuestionsApiService>();
                services.AddSingleton<IProfileQuestionsApiService>(questions ?? new FakeProfileQuestionsApiService());
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        await test(client);
    }

    private async Task<string> GetPageAsync(ManageProfileViewModel profile, FakeProfileSectionsApiService? sections = null)
    {
        var body = string.Empty;

        await WithPageAsync(profile, async client => body = await client.GetStringAsync(PagePath), sections: sections);

        return body;
    }

    // Each question's edit references, validity and help icons sit at the right of its heading, in a row of their own: the accordion
    // opens and closes a question from its header, so an icon inside the header would toggle the question whenever it was clicked.
    [Fact]
    public async Task QuestionIcons_SitBesideEachHeading_NotInsideTheHeaderTheAccordionToggles()
    {
        var body = await GetPageAsync(DraftProfile());

        var row = body.IndexOf("<div class=\"app-question-section-header\">", StringComparison.Ordinal);
        var header = body.IndexOf("<div class=\"govuk-accordion__section-header\">", row, StringComparison.Ordinal);
        var icons = body.IndexOf("<div class=\"app-question-status-icons\">", row, StringComparison.Ordinal);
        var content = body.IndexOf("class=\"govuk-accordion__section-content\"", row, StringComparison.Ordinal);

        Assert.True(row >= 0, "Each question's header and icons should share a row.");
        Assert.True(header > row && icons > header && content > icons, "The icons should follow the header and precede the answers.");

        var headerMarkup = body[header..icons];
        Assert.Equal(CountOf(headerMarkup, "<div"), CountOf(headerMarkup, "</div>"));
    }

    [Fact]
    public async Task QuestionIcons_OfferEditReferencesAndHelp_AsButtonsNamingTheirQuestion()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains($"<button type=\"button\" class=\"app-question-icon-button\" data-question-references=\"{QuestionId}\" data-question-number=\"1.1\" aria-haspopup=\"dialog\" title=\"Edit references\">", body);
        Assert.Contains("alt=\"Edit references for 1.1\"", body);
        Assert.Contains($"<button type=\"button\" class=\"app-question-icon-button\" data-question-help=\"{QuestionId}\" aria-haspopup=\"dialog\" title=\"Help\">", body);
        Assert.Contains("alt=\"Help for 1.1\"", body);
    }

    [Fact]
    public async Task QuestionIcons_OfferNoEditReferences_ToAUserWhoCannotEditTheSection_ButStillOfferHelp()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        Assert.DoesNotContain("data-question-references", body);
        Assert.Contains($"data-question-help=\"{QuestionId}\"", body);
    }

    // The legacy panels opened collapsed whenever the page was opened and kept their state only across the page's own postbacks, so
    // what the accordion remembered from an earlier visit is cleared on opening, never when the answers come back from Save.
    [Fact]
    public async Task Accordion_IsMarkedToOpenCollapsed_WhenThePageIsOpened()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains("id=\"profile-questions-accordion\" data-app-collapse-on-open=\"true\"", body);
        Assert.Contains("/js/question-accordion.js", body);
    }

    // The accordion script points each button's aria-controls at {accordion id}-content-{position}, and remembers what is open under it.
    [Fact]
    public async Task Accordion_GivesEachQuestionsContentTheIdItsButtonControls()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains("<div id=\"profile-questions-accordion-content-1\" class=\"govuk-accordion__section-content\">", body);
    }

    [Fact]
    public async Task Accordion_KeepsWhatTheUserOpened_WhenTheAnswersAreSaved()
    {
        string? body = null;

        await WithPageAsync(DraftProfile(), async client =>
        {
            var page = await client.GetStringAsync(PagePath);
            var token = AntiforgeryTokenRegex().Match(page).Groups[1].Value;
            List<KeyValuePair<string, string>> form =
            [
                new("__RequestVerificationToken", token),
                new("profileVersionId", ProfileVersionId.ToString()),
                new("Section", SummarySectionId.ToString()),
                new("lastUpdated", string.Empty)
            ];

            using var response = await client.PostAsync($"{PagePath}?handler=SaveSection", new FormUrlEncodedContent(form));
            body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        Assert.Contains("id=\"profile-questions-accordion\" data-app-collapse-on-open=\"false\"", body);
        Assert.DoesNotContain("data-app-collapse-on-open=\"true\"", body);
    }

    // Legacy right-aligned the Save and Validate buttons under the questions, as it did those above them.
    [Fact]
    public async Task SaveAndValidate_UnderTheQuestions_AreAlignedToTheRight()
    {
        var body = await GetPageAsync(DraftProfile());

        var panel = body.IndexOf("id=\"profile-questions-panel\"", StringComparison.Ordinal);

        Assert.True(panel >= 0);
        Assert.Contains("<div class=\"govuk-button-group govuk-!-margin-top-6 app-button-group--right\">", body[panel..]);
    }

    // The help icon of each question opens its guidance in a dialog, as the legacy page showed it in a pop-up window. The dialog is
    // outside the form, so nothing in it is posted with the answers.
    [Fact]
    public async Task Page_HasTheUserGuidanceDialog_ThatTheHelpIconsOpen()
    {
        var body = await GetPageAsync(DraftProfile());

        var panel = body.IndexOf("id=\"profile-questions-panel\"", StringComparison.Ordinal);
        var formEnd = body.IndexOf("</form>", panel, StringComparison.Ordinal);
        var dialog = body.IndexOf("<dialog class=\"app-modal app-modal--sectioned\" id=\"user-guidance-dialog\" aria-labelledby=\"user-guidance-dialog-title\" data-help-url=\"/SurveillanceProfiles/ProfileQuestionHelp\">", StringComparison.Ordinal);

        Assert.True(dialog > formEnd, "The dialog should follow the form that posts the answers.");
        Assert.Contains("<h2 class=\"govuk-heading-m govuk-!-margin-bottom-0\" id=\"user-guidance-dialog-title\">User guidance</h2>", body);
        Assert.Contains("data-user-guidance-content", body);
        Assert.Contains("data-confirm-close", body[dialog..]);
        Assert.Contains("/js/question-help.js", body);
    }

    [Fact]
    public async Task Help_ReturnsJustTheQuestionsNameAndItsGuidance_ForTheDialog()
    {
        var questions = new FakeProfileQuestionsApiService(question: new ProfileQuestionEditDto
        {
            Id = QuestionId,
            Name = "Is <i>Brucella</i> present?",
            UserGuidance = "<p>Say <em>yes</em> if it is.</p><script>alert(1)</script>"
        });
        var body = string.Empty;

        await WithPageAsync(DraftProfile(), async client => body = await client.GetStringAsync($"{HelpPath}?profileQuestionId={QuestionId}"), questions);

        Assert.Contains("<h3 class=\"govuk-heading-s\">Is <i>Brucella</i> present?</h3>", body);
        Assert.Contains("<div class=\"govuk-body\"><p>Say <em>yes</em> if it is.</p></div>", body);
        Assert.DoesNotContain("<script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Help_SaysSo_WhenTheQuestionHasNoGuidance()
    {
        var questions = new FakeProfileQuestionsApiService(question: new ProfileQuestionEditDto { Id = QuestionId, Name = "Is it endemic?", UserGuidance = "<p></p>" });
        var body = string.Empty;

        await WithPageAsync(DraftProfile(), async client => body = await client.GetStringAsync($"{HelpPath}?profileQuestionId={QuestionId}"), questions);

        Assert.Contains("<h3 class=\"govuk-heading-s\">Is it endemic?</h3>", body);
        Assert.Contains("<p class=\"govuk-body\">There is no user guidance for this question.</p>", body);
    }

    [Fact]
    public async Task Help_IsNotFound_WhenThereIsNoSuchQuestion()
    {
        HttpStatusCode? status = null;

        await WithPageAsync(DraftProfile(), async client =>
        {
            using var response = await client.GetAsync($"{HelpPath}?profileQuestionId={Guid.NewGuid()}");
            status = response.StatusCode;
        });

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task Help_IsABadGateway_WhenCdcApiCannotBeReached()
    {
        HttpStatusCode? status = null;

        await WithPageAsync(
            DraftProfile(),
            async client =>
            {
                using var response = await client.GetAsync($"{HelpPath}?profileQuestionId={QuestionId}");
                status = response.StatusCode;
            },
            new FakeProfileQuestionsApiService(throwOnGet: new HttpRequestException("boom")));

        Assert.Equal(HttpStatusCode.BadGateway, status);
    }

    // The edit references icon of each question opens the "Add/Select a reference to question" dialog, as the legacy page's pop-up. It is
    // outside the form that posts the answers, and posts to the references page, which answers in JSON.
    [Fact]
    public async Task Page_HasTheReferencesDialog_ThatTheEditReferencesIconsOpen()
    {
        var body = await GetPageAsync(DraftProfile());

        var panel = body.IndexOf("id=\"profile-questions-panel\"", StringComparison.Ordinal);
        var formEnd = body.IndexOf("</form>", panel, StringComparison.Ordinal);
        var dialog = body.IndexOf("<dialog class=\"app-modal app-modal--sectioned app-reference-dialog\" id=\"question-reference-dialog\"", StringComparison.Ordinal);

        Assert.True(dialog > formEnd, "The dialog should follow the form that posts the answers.");
        var dialogMarkup = body[dialog..];
        Assert.Contains($"data-notes-url=\"/SurveillanceProfiles/QuestionReferences/{ProfileId}?section={SummarySectionId}&amp;handler=Notes\"", dialogMarkup);
        Assert.Contains($"action=\"/SurveillanceProfiles/QuestionReferences/{ProfileId}?section={SummarySectionId}&amp;handler=Save\"", dialogMarkup);
        Assert.Contains("name=\"__RequestVerificationToken\"", dialogMarkup);
        Assert.Contains("id=\"question-reference-dialog-title\">Add/Select a reference to question <span data-reference-question-number></span></h2>", dialogMarkup);
        Assert.Contains("<option value=\"add\">Add a reference</option>", dialogMarkup);
        Assert.Contains("<option value=\"select\">Select a reference</option>", dialogMarkup);
        Assert.Contains($"<option value=\"{NoteTypeId}\">Scientific paper reference</option>", dialogMarkup);
        Assert.Contains($"data-reference-question-choice=\"{QuestionId}\" data-reference-choice-number=\"1.1\"", dialogMarkup);
        Assert.Contains(">1.1: ", dialogMarkup);
        Assert.Contains("data-error-mode=\"Select an action\"", dialogMarkup);
        Assert.Contains("/js/question-references.js", body);
    }

    // The legacy pop-up's picker, "Apply this reference also to:", is collapsed, with an All at the top of the questions; the box that shows the
    // text of the reference selected is in the dialog from the start, not made to appear.
    [Fact]
    public async Task ReferencesDialog_HasACollapsedPickerWithAll_AndTheBoxForTheReferenceSelected()
    {
        var body = await GetPageAsync(DraftProfile());

        var dialogMarkup = body[body.IndexOf("id=\"question-reference-dialog\"", StringComparison.Ordinal)..];
        Assert.Contains("<details class=\"govuk-details app-reference-picker\" data-reference-questions>", dialogMarkup);
        Assert.DoesNotContain("data-reference-questions open", dialogMarkup);
        Assert.Contains("Apply this reference also to: <span data-reference-questions-summary></span>", dialogMarkup);
        Assert.Contains("id=\"reference-questions-all\" type=\"checkbox\" data-reference-all />", dialogMarkup);
        Assert.Contains("for=\"reference-questions-all\">All</label>", dialogMarkup);
        Assert.DoesNotContain("name=\"reference-questions-all\"", dialogMarkup);
        Assert.Contains("data-reference-current-note></div>", dialogMarkup);
        Assert.DoesNotContain("data-reference-current-note hidden", dialogMarkup);
    }

    // The legacy page showed "Reference was saved successfully" in lblErrorDesc, so the page has a banner for the dialog to show.
    [Fact]
    public async Task Page_HasAHiddenBanner_ForTheDialogToShowASavedReferenceIn()
    {
        var body = await GetPageAsync(DraftProfile());

        Assert.Contains("id=\"reference-saved-banner\" hidden>", body);
        Assert.Contains("data-reference-saved-message", body);
    }

    [Fact]
    public async Task ReferencesDialog_SaysTheTypesCouldNotBeLoaded_AndOffersNoForm_ButTheIconStillOpensIt()
    {
        var body = await GetPageAsync(DraftProfile(), CreateApi(throwOnGetNoteTypes: new HttpRequestException("boom")));

        Assert.Contains("We could not load the reference types. Try again later.", body);
        Assert.DoesNotContain("data-reference-form", body);
        Assert.Contains("id=\"question-reference-dialog\"", body);
        Assert.Contains("data-question-references=", body);
    }

    [Fact]
    public async Task ReferencesDialog_IsNotOffered_ToAUserWhoCannotEditTheSection()
    {
        var body = await GetPageAsync(ReadOnlyDraftProfile());

        Assert.DoesNotContain("question-reference-dialog", body);
        Assert.DoesNotContain("reference-saved-banner", body);
    }

    [Fact]
    public async Task References_ListsTheVersionsReferencesOfAType_AsJson()
    {
        var json = string.Empty;

        await WithPageAsync(
            DraftProfile(),
            async client => json = await client.GetStringAsync($"{ReferencesPath}?handler=Notes&section={SummarySectionId}&noteTypeId={NoteTypeId}"));

        var notes = JsonSerializer.Deserialize<List<QuestionReferenceNoteOption>>(json, JsonSerializerOptions.Web)!;
        var note = Assert.Single(notes);
        Assert.Equal(NoteId, note.Id);
        Assert.Equal("Smith 2020 Bovine", note.Text);
        Assert.Equal("<p>Smith 2020 <em>Bovine</em></p>", note.Html);
        Assert.Equal(Convert.ToBase64String(RowVersion), note.LastUpdated);
        Assert.Equal([QuestionId], note.QuestionIds);
    }

    [Fact]
    public async Task References_AreNotListed_ToAUserWhoCannotEditTheSection()
    {
        HttpStatusCode? status = null;

        await WithPageAsync(
            ReadOnlyDraftProfile(),
            async client =>
            {
                using var response = await client.GetAsync($"{ReferencesPath}?handler=Notes&section={SummarySectionId}&noteTypeId={NoteTypeId}");
                status = response.StatusCode;
            });

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    private async Task<(HttpStatusCode Status, QuestionReferenceSaveResponse? Response)> PostReferenceAsync(
        ManageProfileViewModel profile,
        IEnumerable<KeyValuePair<string, string>> fields,
        FakeProfileSectionsApiService? sections = null,
        bool withToken = true)
    {
        var status = HttpStatusCode.OK;
        QuestionReferenceSaveResponse? response = null;

        await WithPageAsync(
            profile,
            async client =>
            {
                var token = AntiforgeryTokenRegex().Match(await client.GetStringAsync(PagePath)).Groups[1].Value;
                List<KeyValuePair<string, string>> form = [.. fields];

                if (withToken)
                {
                    form.Add(new("__RequestVerificationToken", token));
                }

                using var reply = await client.PostAsync($"{ReferencesPath}?handler=Save&section={SummarySectionId}", new FormUrlEncodedContent(form));
                status = reply.StatusCode;
                response = reply.IsSuccessStatusCode
                    ? JsonSerializer.Deserialize<QuestionReferenceSaveResponse>(await reply.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)
                    : null;
            },
            sections: sections);

        return (status, response);
    }

    private static List<KeyValuePair<string, string>> NewReferenceFields(string noteText = "<p>New paper</p>") =>
    [
        new("Input.QuestionId", QuestionId.ToString()),
        new("Input.Mode", "add"),
        new("Input.NoteTypeId", NoteTypeId.ToString()),
        new("Input.NoteText", noteText)
    ];

    [Fact]
    public async Task References_SavesANewReference_AndTellsTheDialogSo()
    {
        var sections = CreateApi();

        var (status, response) = await PostReferenceAsync(DraftProfile(), NewReferenceFields(), sections);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(response!.Succeeded);
        Assert.Equal("Reference was saved successfully", response.Message);
        var insert = Assert.Single(sections.SavedNoteInserts!);
        Assert.Equal("<p>New paper</p>", insert.NoteText);
        Assert.Equal(NoteTypeId, sections.ChangedNotesNoteTypeId);
        Assert.Equal([QuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public async Task References_TellTheDialogWhatIsMissing_AndSaveNothing()
    {
        var sections = CreateApi();

        var (status, response) = await PostReferenceAsync(DraftProfile(), NewReferenceFields(noteText: "  "), sections);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(response!.Succeeded);
        Assert.Equal(["Enter the reference text"], response.Errors);
        Assert.Equal(0, sections.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task References_AreNotSaved_ForAUserWhoCannotEditTheSection()
    {
        var sections = CreateApi();

        var (status, _) = await PostReferenceAsync(ReadOnlyDraftProfile(), NewReferenceFields(), sections);

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(0, sections.UpdateProfileNotesCallCount);
    }

    // Razor Pages validates an antiforgery token on every POST, so a reference cannot be saved by a request that did not come from the page.
    [Fact]
    public async Task References_AreNotSaved_WithoutTheAntiforgeryToken()
    {
        var sections = CreateApi();

        var (status, _) = await PostReferenceAsync(DraftProfile(), NewReferenceFields(), sections, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, sections.UpdateProfileNotesCallCount);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
