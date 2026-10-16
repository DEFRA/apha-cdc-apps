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

// Posts the Further information panels through the real Razor Pages pipeline (antiforgery, model binding of the
// repeated question checkboxes, Post-Redirect-Get, the error summary and page title), with a fake standing in for CDC.Api.
public partial class EditProfileFurtherInformationIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileFurtherInformation/{ProfileId}";
    private static readonly Guid NoteTypeId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid LinkedNoteId = Guid.NewGuid();
    private static readonly Guid MasterListNoteId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string RowVersionText = Convert.ToBase64String(RowVersion);

    private readonly CdcWebTestFactory _factory;

    public EditProfileFurtherInformationIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private static FakeProfileSectionsApiService CreateApi(ProfileNoteChangeResult? changeResult = null)
    {
        ProfileNoteDto linked = new()
        {
            Id = LinkedNoteId,
            NoteText = "<p>Linked source</p>",
            LastUpdated = RowVersion,
            QuestionReferences = [new QuestionReferenceDto { ProfileSectionId = SummarySectionId, ProfileQuestionId = QuestionId }]
        };
        ProfileNoteDto masterOnly = new() { Id = MasterListNoteId, NoteText = "<p>Master list source</p>", LastUpdated = RowVersion };

        return new FakeProfileSectionsApiService(
            metadata: TwoSectionMetadata(),
            noteTypes: [new ProfileNoteTypeDto { Id = NoteTypeId, Name = "Source of further information", PluralName = "Sources of further information" }],
            notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [NoteTypeId] = [linked] },
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [NoteTypeId] = [linked, masterOnly] },
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

    // What the browser posts for a panel: the hidden state fields, the panel's own fields and one value per ticked question.
    private static List<KeyValuePair<string, string>> PanelForm(
        string? token,
        string mode,
        string? noteText = null,
        Guid? noteId = null,
        string lastUpdated = "",
        IEnumerable<Guid>? questionIds = null)
    {
        List<KeyValuePair<string, string>> form =
        [
            new("Section", SummarySectionId.ToString()),
            new("PageNumber", "1"),
            new("PageSize", "10"),
            new("SortColumn", "Reference"),
            new("SortDirection", "Ascending"),
            new("Input.Mode", mode),
            new("Input.NoteId", noteId?.ToString() ?? string.Empty),
            new("Input.LastUpdated", lastUpdated)
        ];

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

    [Fact]
    public async Task Add_PostsTheNoteLinkedToEveryTickedQuestion_ThenShowsTheTableWithASuccessMessage()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add");
            var form = PanelForm(token, "add", "<p>Brand new source</p>", questionIds: [QuestionId, SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PagePath, response.RequestMessage!.RequestUri!.AbsolutePath);
            Assert.Contains("result=saved", response.RequestMessage.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("Source of further information save succeeded", body);
            var insert = Assert.Single(api.SavedNoteInserts!);
            Assert.Equal("<p>Brand new source</p>", insert.NoteText);
            Assert.Equal([QuestionId, SecondQuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Fact]
    public async Task Add_ShowsTheErrorSummaryInlineErrorsAndAnErrorTitle_WhenTheSubmissionIsIncomplete()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add");

            using var response = await PostAsync(client, "Save", PanelForm(token, "add", "   "));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<title>Error: Title: Bovine tuberculosis - D2R2</title>", body);
            Assert.Contains("There is a problem", body);
            Assert.True(
                body.IndexOf("There is a problem", StringComparison.Ordinal) < body.IndexOf("Add a source of further information", StringComparison.Ordinal),
                "The error summary should come before the panel it is about.");
            Assert.Contains("<a href=\"#Input_NoteText\">Enter a source of further information</a>", body);
            Assert.Contains("<a href=\"#Input_QuestionIds_0\">Select at least one question</a>", body);
            Assert.Contains("govuk-form-group--error", body);
            Assert.Contains("id=\"Input_NoteText-error\" class=\"govuk-error-message\"", body);
            Assert.Contains("id=\"Input_QuestionIds-error\" class=\"govuk-error-message\"", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Select_LinksTheChosenMasterListSourceToTheTickedQuestion()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?mode=select&noteId={MasterListNoteId}");
            var form = PanelForm(token, "select", noteId: MasterListNoteId, lastUpdated: RowVersionText, questionIds: [SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains("Source of further information save succeeded", body);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal(MasterListNoteId, update.Id);
            Assert.Equal(RowVersion, update.LastUpdated);
            Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
            Assert.Empty(update.QuestionReferenceRemoves);
        });
    }

    [Fact]
    public async Task View_ChangesWhichQuestionsTheSourceIsLinkedTo()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, $"?noteId={LinkedNoteId}");
            var form = PanelForm(token, "view", noteId: LinkedNoteId, lastUpdated: RowVersionText, questionIds: [SecondQuestionId]);

            using var response = await PostAsync(client, "Save", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
            Assert.Equal([QuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Fact]
    public async Task Save_ReportsTheConflictAndKeepsThePanelOpen_WhenAnotherUserChangedTheSource()
    {
        var api = CreateApi(new ProfileNoteChangeResult
        {
            Outcome = ProfileNoteChangeOutcome.Conflict,
            ErrorMessage = "Another user has changed this note."
        });

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add");
            var form = PanelForm(token, "add", "<p>Typed text</p>", questionIds: [QuestionId]);

            using var response = await PostAsync(client, "Save", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<li>Source of further information save failed: Another user has changed this note.</li>", body);
            Assert.Contains("<title>Error: ", body);
            Assert.Contains("&lt;p&gt;Typed text&lt;/p&gt;", body);
        });
    }

    [Fact]
    public async Task Save_ReturnsBadRequest_ForAnUnknownPanel()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client);

            using var response = await PostAsync(client, "Save", PanelForm(token, "bogus"));

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
            var form = PanelForm(token: null, "add", "<p>No token</p>", questionIds: [QuestionId]);

            using var response = await PostAsync(client, "Save", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task Delete_UnlinksTheSourceFromTheSection_ThenShowsTheTableWithASuccessMessage()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client);
            List<KeyValuePair<string, string>> form =
            [
                new("__RequestVerificationToken", token),
                new("noteId", LinkedNoteId.ToString()),
                new("lastUpdated", RowVersionText),
                new("Section", SummarySectionId.ToString()),
                new("PageNumber", "1"),
                new("PageSize", "10"),
                new("SortColumn", "Reference"),
                new("SortDirection", "Ascending")
            ];

            using var response = await PostAsync(client, "Delete", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Source of further information deletion succeeded", body);
            var update = Assert.Single(api.SavedNoteUpdates!);
            Assert.Equal(LinkedNoteId, update.Id);
            Assert.Empty(update.QuestionReferenceAdds);
            Assert.Equal([QuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
        });
    }

    [Fact]
    public async Task TickAll_TicksEveryQuestion_AndKeepsTheTextThatWasTyped()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add");

            using var response = await PostAsync(client, "TickAll", PanelForm(token, "add", "Typed but not saved"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, Regex.Count(body, "checked=\"checked\""));
            Assert.Contains("Typed but not saved</textarea>", body);
            Assert.Equal(0, api.UpdateProfileNotesCallCount);
        });
    }

    [Fact]
    public async Task UntickAll_ClearsEveryTick()
    {
        var api = CreateApi();

        await WithPageAsync(api, async client =>
        {
            var token = await GetTokenAsync(client, "?mode=add");
            var form = PanelForm(token, "add", "Typed", questionIds: [QuestionId, SecondQuestionId]);

            using var response = await PostAsync(client, "UntickAll", form);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("checked=\"checked\"", body);
        });
    }
}
