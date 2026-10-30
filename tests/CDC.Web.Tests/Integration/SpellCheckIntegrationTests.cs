using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Features.SpellCheck;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.SpellCheck;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The Spell check dialog end to end: the page it opens from, then the requests the script makes, through the real pipeline - routing,
// authorization, antiforgery, JSON binding and the real English dictionary. Only CDC.Api is faked.
public partial class SpellCheckIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private const string CheckPath = "/SpellCheck/Check";
    private const string SuggestPath = "/SpellCheck/Suggest";
    private const string AddCustomWordPath = "/SpellCheck/AddCustomWord";
    private const string InventedWord = "Wibbleflorp";

    private static readonly string[] ActionPaths = [CheckPath, SuggestPath, AddCustomWordPath];

    // Two right words, two wrong ones, and one of those twice.
    private static readonly string[] SomeWords = ["cattle", "cattel", "tuberculosis", "tubercolosis", "cattel"];

    // Carries what each of the three requests reads, as one body serves them all.
    private static readonly object AnyBody = new { words = new[] { InventedWord }, word = InventedWord };

    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid TextFieldId = Guid.NewGuid();
    private static readonly Guid LongTextFieldId = Guid.NewGuid();

    private static readonly string PagePath = $"/SurveillanceProfiles/EditProfileQuestions/{ProfileTabTestData.ProfileId}";

    private readonly CdcWebTestFactory _factory;

    public SpellCheckIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task APageWithATextFieldCarriesTheDialogOnce_WithItsButtonsAndAddressesAndTheScript_AndAButtonForEveryTextField()
    {
        using var factory = CreateFactory(new FakeCustomWordsApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync(PagePath);

        Assert.Equal(1, Regex.Count(page, "id=\"spell-check-dialog\""));
        Assert.Contains("data-check-url=\"/SpellCheck/Check\"", page);
        Assert.Contains("data-suggest-url=\"/SpellCheck/Suggest\"", page);
        Assert.Contains("data-add-word-url=\"/SpellCheck/AddCustomWord\"", page);
        Assert.Matches(HeaderNameRegex(), page);
        Assert.Matches(RequestTokenRegex(), page);
        var buttons = new Dictionary<string, string>
        {
            ["ignore"] = "Ignore",
            ["ignore-all"] = "Ignore All",
            ["add-custom"] = "Add Custom",
            ["change"] = "Change",
            ["change-all"] = "Change All",
            ["undo"] = "Undo",
            ["close"] = "Close"
        };
        foreach (var (action, label) in buttons)
        {
            Assert.Contains($"data-spell-action=\"{action}\">{label}</button>", page);
        }

        Assert.Contains("/js/spell-check.js", page);
        Assert.Equal(2, Regex.Count(page, "data-spellcheck-target="));
        Assert.Contains($"data-spellcheck-target=\"field_{TextFieldId}\"", page);
        Assert.Contains($"data-spellcheck-target=\"field_{LongTextFieldId}\"", page);
    }

    [Fact]
    public async Task APageWithNoSpellCheckButtonHasNeitherTheDialogNorTheScript()
    {
        var client = await _factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/Landing/Internal");

        Assert.DoesNotContain("spell-check-dialog", page);
        Assert.DoesNotContain("/js/spell-check.js", page);
    }

    [Fact]
    public async Task Check_FindsTheMisspeltWordsOnly_ForTheTokenTheDialogCarries()
    {
        using var factory = CreateFactory(new FakeCustomWordsApiService());
        var client = await factory.SignedInClientAsync();
        var tokens = await LoadDialogTokensAsync(client);

        var response = await PostAsync(client, CheckPath, new { words = SomeWords }, tokens);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(["cattel", "tubercolosis"], (await response.Content.ReadFromJsonAsync<CheckWordsResponse>())!.Misspelt);
    }

    [Fact]
    public async Task Suggest_OffersTheCorrection_ForTheTokenTheDialogCarries()
    {
        using var factory = CreateFactory(new FakeCustomWordsApiService());
        var client = await factory.SignedInClientAsync();
        var tokens = await LoadDialogTokensAsync(client);

        var response = await PostAsync(client, SuggestPath, new { word = "cattel" }, tokens);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cattle", (await response.Content.ReadFromJsonAsync<SuggestWordResponse>())!.Suggestions);
    }

    [Fact]
    public async Task AddCustomWord_MakesTheWordCorrectForTheNextCheck()
    {
        var customWords = new FakeCustomWordsApiService();
        using var factory = CreateFactory(customWords);
        var client = await factory.SignedInClientAsync();
        var tokens = await LoadDialogTokensAsync(client);

        var before = await MisspeltAsync(client, tokens, InventedWord);
        var added = await PostAsync(client, AddCustomWordPath, new { word = InventedWord }, tokens);
        var after = await MisspeltAsync(client, tokens, InventedWord);

        Assert.Equal([InventedWord], before);
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.Equal([InventedWord], customWords.Words);
        Assert.Empty(after);
    }

    [Fact]
    public async Task AddCustomWord_ForAUserWhoMayNotAddWords_IsRefusedAndTheWordStaysMisspelt()
    {
        var customWords = new FakeCustomWordsApiService(AddCustomWordOutcome.Forbidden);
        using var factory = CreateFactory(customWords);
        var client = await factory.SignedInClientAsync();
        var tokens = await LoadDialogTokensAsync(client);

        var added = await PostAsync(client, AddCustomWordPath, new { word = InventedWord }, tokens);
        var after = await MisspeltAsync(client, tokens, InventedWord);

        Assert.Equal(HttpStatusCode.Forbidden, added.StatusCode);
        Assert.Empty(customWords.Words);
        Assert.Equal([InventedWord], after);
    }

    [Fact]
    public async Task EveryAction_IsRefusedWithoutTheTokenTheDialogCarries_AndAddsNothing()
    {
        var customWords = new FakeCustomWordsApiService();
        using var factory = CreateFactory(customWords);
        var client = await factory.SignedInClientAsync();
        var tokens = await LoadDialogTokensAsync(client);

        foreach (var path in ActionPaths)
        {
            // The page has been loaded, so the antiforgery cookie is there: only the header is missing or wrong.
            var withoutToken = await PostAsync(client, path, AnyBody);
            var wrongToken = await PostAsync(client, path, AnyBody, tokens with { RequestToken = "not-the-token" });

            Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, wrongToken.StatusCode);
        }

        Assert.Empty(customWords.Words);
    }

    [Fact]
    public async Task EveryAction_IsForSignedInUsersOnly()
    {
        var customWords = new FakeCustomWordsApiService();
        using var factory = CreateFactory(customWords);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in ActionPaths)
        {
            var response = await PostAsync(client, path, AnyBody);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Empty(customWords.Words);
    }

    private static async Task<IReadOnlyList<string>> MisspeltAsync(HttpClient client, DialogTokens tokens, string word)
    {
        var response = await PostAsync(client, CheckPath, new { words = new[] { word } }, tokens);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CheckWordsResponse>())!.Misspelt;
    }

    // Loads the page the dialog is on first, as a browser would, to pick up the antiforgery cookie and the token the script sends.
    private static async Task<DialogTokens> LoadDialogTokensAsync(HttpClient client)
    {
        var page = await client.GetStringAsync(PagePath);
        var header = HeaderNameRegex().Match(page);
        var token = RequestTokenRegex().Match(page);
        Assert.True(header.Success && token.Success, "No antiforgery token found on the spell check dialog.");

        return new DialogTokens(WebUtility.HtmlDecode(header.Groups[1].Value), WebUtility.HtmlDecode(token.Groups[1].Value));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, DialogTokens? tokens = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        if (tokens is not null)
        {
            request.Headers.Add(tokens.HeaderName, tokens.RequestToken);
        }

        return await client.SendAsync(request);
    }

    private WebApplicationFactory<Program> CreateFactory(FakeCustomWordsApiService customWords) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: ProfileTabTestData.DraftProfile()));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata: QuestionnaireWithTextFields()));
                services.RemoveAll<ICustomWordsApiService>();
                services.AddSingleton<ICustomWordsApiService>(customWords);
            }));

    // One standard question, whose editable "Text" and "Long Text" fields each get a Spell check button; the Boolean field gets none.
    private static ProfileQuestionnaireMetadataDto QuestionnaireWithTextFields() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SectionId,
                Name = "Summary",
                ShortName = "SUMMARY",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SectionId,
                        ShortName = "Overview",
                        QuestionNumber = 1,
                        Fields =
                        [
                            new ProfileFieldMetadataDto { Id = Guid.NewGuid(), QuestionId = QuestionId, Name = "Is it endemic?", FieldNumber = 1, DataTypeName = "Boolean" },
                            new ProfileFieldMetadataDto { Id = TextFieldId, QuestionId = QuestionId, Name = "Local name", FieldNumber = 2, DataTypeName = "Text" },
                            new ProfileFieldMetadataDto { Id = LongTextFieldId, QuestionId = QuestionId, Name = "Description", FieldNumber = 3, DataTypeName = "Long Text" }
                        ]
                    }
                ]
            }
        ]
    };

    [GeneratedRegex("data-antiforgery-header=\"([^\"]+)\"")]
    private static partial Regex HeaderNameRegex();

    [GeneratedRegex("data-antiforgery-token=\"([^\"]+)\"")]
    private static partial Regex RequestTokenRegex();

    private sealed record DialogTokens(string HeaderName, string RequestToken);

    // CDC.Api's custom dictionary, which keeps the words it is given.
    private sealed class FakeCustomWordsApiService(AddCustomWordOutcome outcome = AddCustomWordOutcome.Added) : ICustomWordsApiService
    {
        private readonly List<string> _words = [];

        public IReadOnlyList<string> Words => _words;

        public Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([.. _words]);

        public Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken = default)
        {
            if (outcome == AddCustomWordOutcome.Added)
            {
                _words.Add(word);
            }

            return Task.FromResult(outcome);
        }
    }
}
