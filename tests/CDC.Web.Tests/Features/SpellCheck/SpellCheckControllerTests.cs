using System.Reflection;
using CDC.Web.Features.SpellCheck;
using CDC.Web.Infrastructure.SpellCheck;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CDC.Web.Tests.Features.SpellCheck;

public class SpellCheckControllerTests
{
    private static SpellCheckController CreateController(FakeSpellCheckService service, bool validAntiforgeryToken = true) =>
        new(service, new FakeAntiforgery { RequestIsValid = validAntiforgeryToken })
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task Check_ReturnsTheMisspeltWords()
    {
        var service = new FakeSpellCheckService { Misspelt = ["cattel"] };

        var result = await CreateController(service).Check(new CheckWordsRequest(["cattel", "the"]), CancellationToken.None);

        var response = Assert.IsType<CheckWordsResponse>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(["cattel"], response.Misspelt);
        Assert.Equal(["cattel", "the"], service.CheckedWords);
    }

    [Fact]
    public async Task Check_AcceptsTheMostWordsAndNoWords()
    {
        var service = new FakeSpellCheckService();
        var controller = CreateController(service);

        var most = await controller.Check(new CheckWordsRequest([.. Enumerable.Repeat("a", SpellCheckController.MaxWords)]), CancellationToken.None);
        var none = await controller.Check(new CheckWordsRequest([]), CancellationToken.None);

        Assert.IsType<JsonResult>(most);
        Assert.IsType<JsonResult>(none);
    }

    public static TheoryData<CheckWordsRequest?> BadCheckRequests =>
    [
        null,
        new CheckWordsRequest(null),
        new CheckWordsRequest([.. Enumerable.Repeat("a", SpellCheckController.MaxWords + 1)]),
        new CheckWordsRequest(["cattle", null!])
    ];

    [Theory]
    [MemberData(nameof(BadCheckRequests))]
    public async Task Check_RefusesARequestThatIsNotAListOfWords(CheckWordsRequest? request)
    {
        var service = new FakeSpellCheckService();

        var result = await CreateController(service).Check(request, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(service.CheckedWords);
    }

    [Fact]
    public async Task Suggest_ReturnsTheSuggestions()
    {
        var service = new FakeSpellCheckService { Suggestions = ["cattle", "battle"] };

        var result = await CreateController(service).Suggest(new SuggestWordRequest("cattel"), CancellationToken.None);

        var response = Assert.IsType<SuggestWordResponse>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(["cattle", "battle"], response.Suggestions);
        Assert.Equal("cattel", service.SuggestedWord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Suggest_RefusesARequestWithNoWord(string? word)
    {
        var service = new FakeSpellCheckService();

        var result = await CreateController(service).Suggest(new SuggestWordRequest(word), CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(service.SuggestedWord);
    }

    [Fact]
    public async Task Suggest_RefusesANullRequest_AndAWordLongerThanTheTableHolds()
    {
        var service = new FakeSpellCheckService();
        var controller = CreateController(service);

        var noRequest = await controller.Suggest(null, CancellationToken.None);
        var tooLong = await controller.Suggest(new SuggestWordRequest(new string('a', SpellCheckController.MaxWordLength + 1)), CancellationToken.None);

        Assert.IsType<BadRequestResult>(noRequest);
        Assert.IsType<BadRequestResult>(tooLong);
        Assert.Null(service.SuggestedWord);
    }

    [Theory]
    [InlineData(AddCustomWordOutcome.Added, StatusCodes.Status204NoContent)]
    [InlineData(AddCustomWordOutcome.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(AddCustomWordOutcome.Invalid, StatusCodes.Status400BadRequest)]
    [InlineData(AddCustomWordOutcome.Failed, StatusCodes.Status502BadGateway)]
    public async Task AddCustomWord_AnswersWithTheOutcome(AddCustomWordOutcome outcome, int expectedStatusCode)
    {
        var service = new FakeSpellCheckService { AddOutcome = outcome };

        var result = await CreateController(service).AddCustomWord(new AddCustomWordRequest("Defra"), CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Equal("Defra", service.AddedWord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddCustomWord_RefusesARequestWithNoWord(string? word)
    {
        var service = new FakeSpellCheckService();
        var controller = CreateController(service);

        var noRequest = await controller.AddCustomWord(null, CancellationToken.None);
        var noWord = await controller.AddCustomWord(new AddCustomWordRequest(word), CancellationToken.None);

        Assert.IsType<BadRequestResult>(noRequest);
        Assert.IsType<BadRequestResult>(noWord);
        Assert.Null(service.AddedWord);
    }

    // Each action is only a POST from the dialog's own page, and the token that says so is checked in the action.
    [Fact]
    public async Task EveryActionRefusesARequestWithoutAValidAntiforgeryToken()
    {
        var service = new FakeSpellCheckService();
        var controller = CreateController(service, validAntiforgeryToken: false);

        var results = new[]
        {
            await controller.Check(new CheckWordsRequest(["cattle"]), CancellationToken.None),
            await controller.Suggest(new SuggestWordRequest("cattel"), CancellationToken.None),
            await controller.AddCustomWord(new AddCustomWordRequest("Defra"), CancellationToken.None)
        };

        Assert.All(results, result => Assert.IsType<BadRequestResult>(result));
        Assert.Empty(service.CheckedWords);
        Assert.Null(service.SuggestedWord);
        Assert.Null(service.AddedWord);
    }

    [Fact]
    public void EveryActionIsAPost()
    {
        var actions = typeof(SpellCheckController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal(3, actions.Length);
        Assert.All(actions, action => Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>()));
    }

    // Any antiforgery attribute makes CodeQL flag every [HttpPost] in the solution, CDC.Api's included, so the check is made in the action.
    [Fact]
    public void NoActionOrControllerCarriesAnAntiforgeryAttribute()
    {
        var attributes = typeof(SpellCheckController).GetCustomAttributes()
            .Concat(typeof(SpellCheckController).GetMethods().SelectMany(method => method.GetCustomAttributes()));

        Assert.DoesNotContain(attributes, attribute => attribute.GetType().Name.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheAnswersAreNeverCached() =>
        Assert.True(typeof(SpellCheckController).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);

    private sealed class FakeSpellCheckService : ISpellCheckService
    {
        public IReadOnlyList<string> Misspelt { get; set; } = [];

        public IReadOnlyList<string> Suggestions { get; set; } = [];

        public AddCustomWordOutcome AddOutcome { get; set; } = AddCustomWordOutcome.Added;

        public List<string> CheckedWords { get; } = [];

        public string? SuggestedWord { get; private set; }

        public string? AddedWord { get; private set; }

        public Task<IReadOnlyList<string>> FindMisspeltWordsAsync(IEnumerable<string> words, CancellationToken cancellationToken)
        {
            CheckedWords.AddRange(words);

            return Task.FromResult(Misspelt);
        }

        public Task<IReadOnlyList<string>> SuggestAsync(string word, CancellationToken cancellationToken)
        {
            SuggestedWord = word;

            return Task.FromResult(Suggestions);
        }

        public Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken)
        {
            AddedWord = word;

            return Task.FromResult(AddOutcome);
        }
    }
}
