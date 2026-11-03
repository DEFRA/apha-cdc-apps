using System.Net;
using CDC.Web.Infrastructure.SpellCheck;

namespace CDC.Web.Tests.Infrastructure.SpellCheck;

public class CustomWordsApiServiceTests
{
    private static CustomWordsApiService CreateService(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    [Fact]
    public async Task GetCustomWordsAsync_ReadsTheWordsFromTheCustomWordsEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """["Defra","APHA"]""");

        var words = await CreateService(handler).GetCustomWordsAsync();

        Assert.Equal(["Defra", "APHA"], words);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/api/spell-check/custom-words", handler.LastUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetCustomWordsAsync_ReturnsNone_WhenTheResponseBodyIsNull()
    {
        var words = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null")).GetCustomWordsAsync();

        Assert.Empty(words);
    }

    [Fact]
    public async Task GetCustomWordsAsync_Throws_OnAFailureStatusCode() =>
        await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateService(new FakeHttpMessageHandler(HttpStatusCode.Forbidden, string.Empty)).GetCustomWordsAsync());

    [Fact]
    public async Task AddCustomWordAsync_PostsTheWordToTheCustomWordsEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        var outcome = await CreateService(handler).AddCustomWordAsync("Defra");

        Assert.Equal(AddCustomWordOutcome.Added, outcome);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/spell-check/custom-words", handler.LastUri!.AbsolutePath);
        Assert.Equal("""{"word":"Defra"}""", handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, AddCustomWordOutcome.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest, AddCustomWordOutcome.Invalid)]
    [InlineData(HttpStatusCode.InternalServerError, AddCustomWordOutcome.Failed)]
    [InlineData(HttpStatusCode.BadGateway, AddCustomWordOutcome.Failed)]
    public async Task AddCustomWordAsync_ReportsWhyTheWordWasRefused(HttpStatusCode status, AddCustomWordOutcome expected)
    {
        var outcome = await CreateService(new FakeHttpMessageHandler(status, string.Empty)).AddCustomWordAsync("Defra");

        Assert.Equal(expected, outcome);
    }
}
