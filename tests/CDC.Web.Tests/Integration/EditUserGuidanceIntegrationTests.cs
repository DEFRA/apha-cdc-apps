using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - routing, authorization, model binding, antiforgery, TempData and the Razor page - for the
// Edit questions and guidance page. Only the CDC.Api clients are faked.
public partial class EditUserGuidanceIntegrationTests
{
    private const string PageUrl = "/SurveillanceProfiles/EditUserGuidance";

    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid FirstQuestionId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Get_ShowsTheSectionPicker_AndTheReportButtons_ButNoQuestionsUntilASectionIsSelected()
    {
        using var factory = CreateFactory(new FakeProfileQuestionsApiService());
        var client = await EditorAsync(factory);

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Edit questions and guidance - D2R2</title>", page);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Edit questions and guidance</h1>", page);
        Assert.Contains(">D2R2 Home</a>", page);
        Assert.Contains("<li class=\"govuk-breadcrumbs__list-item\" aria-current=\"page\">Questions and guidance</li>", page);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(page, "govuk-breadcrumbs__list-item"));
        Assert.Contains("Use the buttons below to generate the user guidance reports.", page);
        Assert.Contains("Full profile guidance report", page);
        Assert.Contains("Summary profile guidance report", page);
        Assert.Contains("SPR guidance report", page);
        Assert.Contains("Q&amp;A sheet guidance report", page);
        foreach (var type in new[] { "All", "SummaryProfile", "SummaryPrioritisationReport", "QAGuidanceReport" })
        {
            Assert.Contains($"href=\"/SurveillanceProfiles/ViewProfileGuidanceReport?type={type}\"", page);
        }

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Count(page, "target=\"_blank\" rel=\"noopener noreferrer\""));
        Assert.Contains("for=\"Section\">Select a profile section</label>", page);
        Assert.Contains("<option value=\"\">Please select...</option>", page);
        Assert.Contains($"<option value=\"{SectionId}\">1: Disease &amp; overview</option>", page);
        Assert.DoesNotContain("<table", page);
        Assert.DoesNotContain("quality statement", page);
        Assert.DoesNotContain("govuk-error-summary", page);
    }

    [Fact]
    public async Task Get_ListsTheQuestionsOfTheSection_WithEditLinks_AndTheQualityStatementNote()
    {
        using var factory = CreateFactory(new FakeProfileQuestionsApiService());
        var client = await EditorAsync(factory);

        var page = await client.GetStringAsync($"{PageUrl}?section={SectionId}");

        Assert.Contains($"<option value=\"{SectionId}\" selected=\"selected\">1: Disease &amp; overview</option>", page);
        Assert.Contains("<td class=\"govuk-table__cell\">1</td>", page);
        Assert.Contains("Is <i>Brucella</i> present?", page);
        Assert.Contains("&lt;script&gt;x&lt;/script&gt;", page);
        Assert.DoesNotContain("<script>x</script>", page);
        Assert.Contains($"href=\"{PageUrl}?section={SectionId}&amp;questionId={FirstQuestionId}#edit-question\"", page);
        Assert.Contains("<span class=\"govuk-visually-hidden\"> question 2</span>", page);
        Assert.Contains("is not scheduled to be updated. Please see the D2R2 quality statement for a list of these questions.", page);
        Assert.DoesNotContain("id=\"edit-question\"", page);
    }

    [Fact]
    public async Task Get_OpensTheEditPanel_WithTheQuestionsTextInEditors()
    {
        var question = new ProfileQuestionEditDto
        {
            Id = FirstQuestionId,
            Name = "Is <i>Brucella</i> present?",
            NonTechnicalName = "Is it <em>here</em>?",
            QuestionNumber = 1,
            UserGuidance = "<p>Say yes.</p>",
            LastUpdated = RowVersion
        };
        using var factory = CreateFactory(new FakeProfileQuestionsApiService(question));
        var client = await EditorAsync(factory);

        var page = await client.GetStringAsync($"{PageUrl}?section={SectionId}&questionId={FirstQuestionId}");

        Assert.Contains("id=\"edit-question\"", page);
        Assert.Contains("<h2 class=\"govuk-heading-m\" id=\"edit-question-heading\">Edit question 1</h2>", page);
        Assert.Contains("data-app-title-toolbar=\"Input_NameEditor\"", page);
        Assert.Contains("data-app-title-toolbar=\"Input_NonTechnicalNameEditor\"", page);
        Assert.Contains("aria-label=\"Title formatting\"", page);
        Assert.Contains("aria-label=\"Non-technical title formatting\"", page);
        Assert.Contains("data-app-title-hidden-input=\"Input_Name\">Is <i>Brucella</i> present?</div>", page);
        Assert.Contains("name=\"Input.Name\" value=\"Is &lt;i&gt;Brucella&lt;/i&gt; present?\"", page);
        Assert.Contains("name=\"Input.NonTechnicalName\" value=\"Is it &lt;em&gt;here&lt;/em&gt;?\"", page);
        Assert.Contains("name=\"Input.UserGuidance\"", page);
        Assert.Contains("&lt;p&gt;Say yes.&lt;/p&gt;</textarea>", page);
        Assert.Contains("data-richtext-command=\"link\" data-richtext-target=\"Input_UserGuidance\"", page);
        Assert.Contains("data-spellcheck-target=\"Input_UserGuidance-editor\"", page);
        Assert.Contains("id=\"spell-check-dialog\"", page);
        Assert.Contains($"name=\"Input.RowVersion\" value=\"{Convert.ToBase64String(RowVersion)}\"", page);
        Assert.Contains($"name=\"QuestionId\" value=\"{FirstQuestionId}\"", page);
        Assert.Contains(">OK</button>", page);
        Assert.Contains($"href=\"{PageUrl}?section={SectionId}\"", page);
        Assert.Contains("/js/edit-profile-title.js", page);
        Assert.Contains("/js/richtext-toolbar.js", page);
        Assert.Contains("/js/spell-check.js", page);
    }

    [Fact]
    public async Task Get_ShowsAnErrorSummary_WhenTheSectionsCannotBeLoaded()
    {
        var sections = new FakeProfileSectionsApiService(throwOnGetProfileQuestionnaireMetadata: new HttpRequestException("boom"));
        using var factory = CreateFactory(new FakeProfileQuestionsApiService(), sections);
        var client = await EditorAsync(factory);

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Error: Edit questions and guidance - D2R2</title>", page);
        Assert.Contains("There is a problem", page);
        Assert.Contains("We could not load the profile sections. Try again later.", page);
    }

    [Fact]
    public async Task Post_WithMissingFields_ShowsTheErrorsInFieldOrder_KeepsThePanelOpen_AndDoesNotSave()
    {
        var questions = new FakeProfileQuestionsApiService(OpenQuestion());
        using var factory = CreateFactory(questions);
        var client = await EditorAsync(factory);

        var response = await PostAsync(client, name: "<p><br></p>", nonTechnicalName: "", guidance: "");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Error: Edit questions and guidance - D2R2</title>", body);
        var summaryLinks = new[] { "<a href=\"#Input_NameEditor\">", "<a href=\"#Input_NonTechnicalNameEditor\">", "<a href=\"#Input_UserGuidance-group\">" };
        var positions = summaryLinks.Select(link => body.IndexOf(link, StringComparison.Ordinal)).ToList();
        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.Order().ToList(), positions);
        Assert.Contains("Enter the question text", body);
        Assert.Contains("Enter the non-technical question text", body);
        Assert.Contains("Enter the user guidance text", body);
        Assert.Contains("id=\"Input_Name-error\"", body);
        Assert.Contains("id=\"edit-question\"", body);
        Assert.Equal(0, questions.UpdateCallCount);
    }

    [Fact]
    public async Task Post_WithValidText_SavesAndShowsTheSuccessMessageOnTheRedirectedList()
    {
        var questions = new FakeProfileQuestionsApiService(OpenQuestion());
        using var factory = CreateFactory(questions);
        var client = await EditorAsync(factory);

        var response = await PostAsync(client, name: "Is <i>it</i> here?", nonTechnicalName: "Here?", guidance: "<p>Say yes.</p>");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{PageUrl}?section={SectionId}", response.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains("govuk-notification-banner--success", body);
        Assert.Contains("User guidance save succeeded", body);
        Assert.DoesNotContain("id=\"edit-question\"", body);
        Assert.Equal(FirstQuestionId, questions.UpdatedQuestionId);
        Assert.Equal("Is <i>it</i> here?", questions.UpdateRequest!.Name);
        Assert.Equal(RowVersion, questions.UpdateRequest.LastUpdated);
    }

    [Fact]
    public async Task Post_WhenTheApiRefusesTheSave_ShowsTheReasonInAnErrorSummaryOnTheRedirectedList()
    {
        var questions = new FakeProfileQuestionsApiService(OpenQuestion(), new ProfileQuestionUpdateResult
        {
            Outcome = ProfileQuestionUpdateOutcome.Conflict,
            ErrorMessage = ProfileQuestionsApiService.ConflictMessage
        });
        using var factory = CreateFactory(questions);
        var client = await EditorAsync(factory);

        var response = await PostAsync(client, name: "Name", nonTechnicalName: "Plain", guidance: "Guidance");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Error: Edit questions and guidance - D2R2</title>", body);
        Assert.Contains("User guidance save failed: The user guidance has been edited by another user", body);
        Assert.DoesNotContain("govuk-notification-banner--success", body);
        Assert.DoesNotContain("id=\"edit-question\"", body);
    }

    [Fact]
    public async Task Post_AsAUserWhoCannotEditGuidance_IsRefused()
    {
        var questions = new FakeProfileQuestionsApiService();
        using var factory = CreateFactory(questions);
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var response = await client.PostAsync(PageUrl, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, questions.UpdateCallCount);
    }

    private static ProfileQuestionEditDto OpenQuestion() => new()
    {
        Id = FirstQuestionId,
        Name = "Name",
        NonTechnicalName = "Plain",
        QuestionNumber = 1,
        UserGuidance = "Guidance",
        LastUpdated = RowVersion
    };

    private static Task<HttpClient> EditorAsync(WebApplicationFactory<Program> factory) =>
        factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);

    // Loads the open edit form first, as a browser would, to pick up the antiforgery token the post needs.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string name, string nonTechnicalName, string guidance)
    {
        var url = $"{PageUrl}?section={SectionId}&questionId={FirstQuestionId}";
        var page = await client.GetStringAsync(url);
        var token = AntiforgeryTokenRegex().Match(page);
        Assert.True(token.Success, "No antiforgery token found on the edit form.");

        return await client.PostAsync(url, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token.Groups[1].Value),
            new KeyValuePair<string, string>("Section", SectionId.ToString()),
            new KeyValuePair<string, string>("QuestionId", FirstQuestionId.ToString()),
            new KeyValuePair<string, string>("Input.RowVersion", Convert.ToBase64String(RowVersion)),
            new KeyValuePair<string, string>("Input.Name", name),
            new KeyValuePair<string, string>("Input.NonTechnicalName", nonTechnicalName),
            new KeyValuePair<string, string>("Input.UserGuidance", guidance)
        ]));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FakeProfileQuestionsApiService questions,
        FakeProfileSectionsApiService? sections = null) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileQuestionsApiService>();
                services.AddSingleton<IProfileQuestionsApiService>(questions);
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(sections ?? DefaultSections());
            }));

    private static FakeProfileSectionsApiService DefaultSections() => new(
        metadata: new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = SectionId, ShortName = "DISEASE AND OVERVIEW", SectionNumber = 1 }]
        },
        questionInfos:
        [
            new ProfileQuestionInfoDto { Id = FirstQuestionId, Name = "Is <i>Brucella</i> present?", QuestionNumber = 1 },
            new ProfileQuestionInfoDto { Id = SecondQuestionId, Name = "<script>x</script>", QuestionNumber = 2 }
        ]);

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
