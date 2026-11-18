using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Integration;

// Posts the References panels through the real Razor Pages pipeline (antiforgery, model binding of the prefixed paging values and the
// repeated question checkboxes, Post-Redirect-Get, the error summary and page title), with a fake standing in for CDC.Api. The tab holds
// a table for scientific paper references and one for legislative references, and every post names the kind it is about.
public partial class EditProfileReferencesIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileReferences/{ProfileId}";
    private static readonly Guid ScientificTypeId = Guid.NewGuid();
    private static readonly Guid LegislativeTypeId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid LinkedPaperId = Guid.NewGuid();
    private static readonly Guid MasterListPaperId = Guid.NewGuid();
    private static readonly Guid LinkedActId = Guid.NewGuid();
    private static readonly Guid MasterListActId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string RowVersionText = Convert.ToBase64String(RowVersion);

    private readonly CdcWebTestFactory _factory;

    public EditProfileReferencesIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private static ProfileNoteDto Linked(Guid id, string text) => new()
    {
        Id = id,
        NoteText = text,
        LastUpdated = RowVersion,
        QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = SummarySectionId, ProfileQuestionId = QuestionId }]
    };

    private static ProfileNoteDto MasterOnly(Guid id, string text) => new() { Id = id, NoteText = text, LastUpdated = RowVersion };

    private static FakeProfileSectionsApiService CreateApi(ProfileNoteChangeResult? changeResult = null)
    {
        var paper = Linked(LinkedPaperId, "<p>Linked paper</p>");
        var act = Linked(LinkedActId, "<p>Linked act</p>");

        return new FakeProfileSectionsApiService(
            metadata: TwoSectionMetadata(),
            noteTypes:
            [
                new ProfileNoteTypeDto { Id = ScientificTypeId, Name = "Scientific paper reference", PluralName = "Scientific paper references" },
                new ProfileNoteTypeDto { Id = LegislativeTypeId, Name = "Legislative reference", PluralName = "Legislative references" }
            ],
            notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [ScientificTypeId] = [paper],
                [LegislativeTypeId] = [act]
            },
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [ScientificTypeId] = [paper, MasterOnly(MasterListPaperId, "<p>Master list paper</p>")],
                [LegislativeTypeId] = [act, MasterOnly(MasterListActId, "<p>Master list act</p>")]
            },
            questionInfos:
            [
                new ProfileQuestionInfoDto { Id = QuestionId, Name = "Is it endemic?", QuestionNumber = 1 },
                new ProfileQuestionInfoDto { Id = SecondQuestionId, Name = "How is it spread?", QuestionNumber = 2 }
            ],
            noteChangeResult: changeResult);
    }

    private async Task WithPageAsync(FakeProfileSectionsApiService api, Func<HttpClient, Task> test)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: DraftProfile()));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(api);
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        await test(client);
    }

    // Razor Pages validates an antiforgery token on every POST, so the page is read first to pick one up (and its cookie).
    private static async Task<string> GetTokenAsync(HttpClient client, string query = "")
    {
        var page = await client.GetStringAsync($"{PagePath}{query}");
        var match = AntiforgeryTokenRegex().Match(page);
        Assert.True(match.Success, "The page should carry an antiforgery token.");

        return match.Groups[1].Value;
    }

    // What the browser posts for a panel: the hidden state fields of both tables, the kind the panel is for, the panel's own fields and
    // one value per ticked question.
    private static List<KeyValuePair<string, string>> PanelForm(
        string? token,
        string? type,
        string mode,
        string? noteText = null,
        Guid? noteId = null,
        string lastUpdated = "",
        IEnumerable<Guid>? questionIds = null)
    {
        List<KeyValuePair<string, string>> form =
        [
            new("section", SummarySectionId.ToString()),
            new("scientific.pageNumber", "1"),
            new("scientific.pageSize", "10"),
            new("scientific.sortColumn", "Reference"),
            new("scientific.sortDirection", "Ascending"),
            new("legislative.pageNumber", "1"),
            new("legislative.pageSize", "25"),
            new("legislative.sortColumn", "QuestionReference"),
            new("legislative.sortDirection", "Descending"),
            new("Input.Mode", mode),
            new("Input.NoteId", noteId?.ToString() ?? string.Empty),
            new("Input.LastUpdated", lastUpdated)
        ];

        if (type is not null)
        {
            form.Add(new("type", type));
        }

        if (token is not null)
        {
            form.Add(new("__RequestVerificationToken", token));
        }

        if (noteText is not null)
        {
            form.Add(new("Input.NoteText", noteText));
        }

        form.AddRange((questionIds ?? []).Select(questionId => new KeyValuePair<string, string>("Input.QuestionIds", questionId.ToString())));

        return form;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string handler, List<KeyValuePair<string, string>> form) =>
        client.PostAsync($"{PagePath}?handler={handler}", new FormUrlEncodedContent(form));

    [Theory]
    [InlineData("scientific", "Scientific paper reference")]
    [InlineData("legislative", "Legislative reference")]
    public async Task Add_PostsTheNoteAsTheKindNamed_ThenShowsBothTablesWithASuccessMessage(string type, string noteTypeName)
    {
        var expectedTypeId = type == "scientific" ? ScientificTypeId : LegislativeTypeId;
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?mode=add&type={type}");
            var form = PanelForm(token, type, "add", "<p>Brand new one</p>", questionIds: [QuestionId, SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PagePath, response.RequestMessage!.RequestUri!.AbsolutePath);
            var query = response.RequestMessage.RequestUri.Query;
            Assert.Contains("result=saved", query, StringComparison.Ordinal);
            Assert.Contains($"type={type}", query, StringComparison.Ordinal);
            Assert.Contains($"{noteTypeName} save succeeded", body);
            Assert.Contains("Linked paper", body);
            Assert.Contains("Linked act", body);
            Assert.Equal(expectedTypeId, api.ChangedNotesNoteTypeId);
            var insert = Assert.Single(api.SavedNoteInserts!);
            Assert.Equal("<p>Brand new one</p>", insert.NoteText);
            Assert.Equal([QuestionId, SecondQuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Fact]
    public async Task Save_KeepsEveryTablesPagingAndSort_ThroughTheRedirect()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add&type=scientific");
            var form = PanelForm(token, "scientific", "add", "<p>Brand new one</p>", questionIds: [QuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            var query = response.RequestMessage!.RequestUri!.Query;
            Assert.Contains("scientific.pageSize=10", query, StringComparison.Ordinal);
            Assert.Contains("legislative.pageSize=25", query, StringComparison.Ordinal);
            Assert.Contains("legislative.sortColumn=QuestionReference", query, StringComparison.Ordinal);
            Assert.Contains("legislative.sortDirection=Descending", query, StringComparison.Ordinal);
            Assert.Contains("<option value=\"25\" selected=\"selected\">25</option>", body);
            Assert.Contains("aria-sort=\"descending\"", body);
        });
    }

    [Fact]
    public async Task Add_ShowsTheErrorSummaryInlineErrorsAsterisksAndAnErrorTitle_WhenTheSubmissionIsIncomplete()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add&type=legislative");

            using var response = await PostAsync(client, "Save", PanelForm(token, "legislative", "add", "   "));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Error: Title: Bovine tuberculosis - D2R2</title>", body);
            Assert.Contains("The following errors have occurred:", body);
            Assert.True(
                body.IndexOf("The following errors have occurred:", StringComparison.Ordinal) < body.IndexOf("Add a legislative reference", StringComparison.Ordinal),
                "The error summary should come before the panel it is about.");
            Assert.Contains("<a href=\"#Input_NoteText\">Please enter a reference</a>", body);
            Assert.Contains("<a href=\"#Input_QuestionIds_0\">At least one question must be selected</a>", body);
            Assert.Contains("govuk-form-group--error", body);
            Assert.Contains("id=\"Input_NoteText-error\" class=\"govuk-error-message\"", body);
            Assert.Contains("id=\"Input_QuestionIds-error\" class=\"govuk-error-message\"", body);
            Assert.Contains("<span class=\"app-validator-marker\" title=\"Please enter a reference\" aria-hidden=\"true\">*</span>", body);
            Assert.Contains("Add a legislative reference</h2>", body);
            Assert.DoesNotContain("Add a scientific paper reference</h2>", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Select_ShowsAnAsteriskBesideTheDropDown_WhenNothingIsChosenButAQuestionIsTicked()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=select&type=scientific");
            var form = PanelForm(token, "scientific", "select", questionIds: [QuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("The following errors have occurred:", body);
            Assert.Contains("<a href=\"#NoteId\">Please select a reference</a>", body);
            Assert.DoesNotContain("At least one question must be selected", body);
            Assert.Contains("govuk-select--error", body);
            Assert.Contains("<span class=\"app-validator-marker\" title=\"Please select a reference\" aria-hidden=\"true\">*</span>", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Select_ReportsThatAQuestionMustBeSelected_WhenANoteIsChosenButNoQuestionIsTicked()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?mode=select&type=scientific&noteId={MasterListPaperId}");
            var form = PanelForm(token, "scientific", "select", noteId: MasterListPaperId, lastUpdated: RowVersionText);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<a href=\"#Input_QuestionIds_0\">At least one question must be selected</a>", body);
            Assert.DoesNotContain("Please select a reference", body);
            Assert.DoesNotContain("app-validator-marker", body);
            Assert.Contains($"<option value=\"{MasterListPaperId}\" selected=\"selected\">Master list paper</option>", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Select_LinksTheChosenMasterListNoteToTheTickedQuestion()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?mode=select&type=legislative&noteId={MasterListActId}");
            var form = PanelForm(token, "legislative", "select", noteId: MasterListActId, lastUpdated: RowVersionText, questionIds: [SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains("Legislative reference save succeeded", body);
            Assert.Equal(LegislativeTypeId, api.ChangedNotesNoteTypeId);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal(MasterListActId, update.Id);
            Assert.Equal(RowVersion, update.LastUpdated);
            Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
            Assert.Empty(update.QuestionReferenceRemoves);
        });
    }

    [Fact]
    public async Task View_ChangesWhichQuestionsTheNoteIsLinkedTo()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?type=scientific&noteId={LinkedPaperId}");
            var form = PanelForm(token, "scientific", "view", noteId: LinkedPaperId, lastUpdated: RowVersionText, questionIds: [SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(ScientificTypeId, api.ChangedNotesNoteTypeId);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
            Assert.Equal([QuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bogus")]
    public async Task Save_ReturnsBadRequest_WhenNoKindOrAnUnknownKindIsNamed(string? type)
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client);

            using var response = await PostAsync(client, "Save", PanelForm(token, type, "add", "<p>Text</p>", questionIds: [QuestionId]));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Save_IsRejected_WithoutAnAntiforgeryToken()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var form = PanelForm(token: null, "scientific", "add", "<p>No token</p>", questionIds: [QuestionId]);

            using var response = await PostAsync(client, "Save", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Delete_UnlinksTheNoteFromTheSection_ThenShowsTheTablesWithASuccessMessage()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client);
            var form = PanelForm(token, "legislative", "view");
            form.RemoveAll(field => field.Key.StartsWith("Input.", StringComparison.Ordinal));
            form.Add(new("noteId", LinkedActId.ToString()));
            form.Add(new("lastUpdated", RowVersionText));

            using var response = await PostAsync(client, "Delete", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("deleted", response.RequestMessage!.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Contains("Legislative reference deletion succeeded", body);
            Assert.Equal(LegislativeTypeId, api.ChangedNotesNoteTypeId);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal(LinkedActId, update.Id);
            Assert.Empty(update.QuestionReferenceAdds);
            Assert.Equal([QuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenNoKindIsNamed()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client);
            List<KeyValuePair<string, string>> form =
            [
                new("__RequestVerificationToken", token),
                new("noteId", LinkedPaperId.ToString()),
                new("lastUpdated", RowVersionText),
                new("section", SummarySectionId.ToString())
            ];

            using var response = await PostAsync(client, "Delete", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task TickAll_TicksEveryQuestionOfThePanelOfTheKindNamed_AndKeepsTheTextThatWasTyped()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add&type=scientific");

            using var response = await PostAsync(client, "TickAll", PanelForm(token, "scientific", "add", "Typed but not saved"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, Regex.Count(body, "checked=\"checked\""));
            Assert.Contains("Typed but not saved</textarea>", body);
            Assert.Contains("Add a scientific paper reference</h2>", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task UntickAll_ClearsEveryTick()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add&type=legislative");
            var form = PanelForm(token, "legislative", "add", "Typed", questionIds: [QuestionId, SecondQuestionId]);

            using var response = await PostAsync(client, "UntickAll", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("checked=\"checked\"", body);
            Assert.Contains("Add a legislative reference</h2>", body);
        });
    }
}
