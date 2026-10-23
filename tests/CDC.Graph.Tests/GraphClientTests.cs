using System.Net;

namespace CDC.Graph.Tests;

public class GraphClientTests
{
    private static GraphClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://graph.example/v1.0/") });

    [Fact]
    public async Task FindUser_ReturnsTheSingleMatch_WithCompanyAsOrganisation()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"value":[{"id":"abc","displayName":"Jo Smith","companyName":"APHA","department":"Ignored"}]}""");

        var user = await CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None);

        Assert.Equal(new GraphUser("abc", "Jo Smith", "APHA"), user);
    }

    [Fact]
    public async Task FindUser_FallsBackToDepartment_WhenThereIsNoCompany()
    {
        var handler = StubHttpMessageHandler.Json("""{"value":[{"id":"abc","displayName":"Jo","department":"Surveillance"}]}""");

        var user = await CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None);

        Assert.Equal("Surveillance", user?.Organisation);
    }

    [Fact]
    public async Task FindUser_LeavesOrganisationNull_WhenNeitherIsSet()
    {
        var handler = StubHttpMessageHandler.Json("""{"value":[{"id":"abc","displayName":"Jo","companyName":" "}]}""");

        var user = await CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None);

        Assert.Null(user?.Organisation);
    }

    [Theory]
    [InlineData("""{"value":[]}""")]
    [InlineData("""{"value":[{"id":"a","displayName":"A"},{"id":"b","displayName":"B"}]}""")]
    [InlineData("""{"value":[{"id":"a","displayName":""}]}""")]
    [InlineData("""{"value":[{"id":"","displayName":"A"}]}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    public async Task FindUser_ReturnsNull_WhenThereIsNoUsableSingleMatch(string json)
    {
        var user = await CreateClient(StubHttpMessageHandler.Json(json)).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task FindUser_SendsAnEscapedFilter_AndTheAdvancedQueryHeader()
    {
        var handler = StubHttpMessageHandler.Json("""{"value":[]}""");

        await CreateClient(handler).FindUserBySamAccountNameAsync("o'brien", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        var query = Uri.UnescapeDataString(request.RequestUri!.Query);
        Assert.Contains("$filter=onPremisesSamAccountName eq 'o''brien'", query, StringComparison.Ordinal);
        Assert.Contains("$top=2", query, StringComparison.Ordinal);
        Assert.Contains("$count=true", query, StringComparison.Ordinal);
        Assert.Equal("eventual", Assert.Single(request.Headers.GetValues("ConsistencyLevel")));
    }

    [Fact]
    public async Task FindUser_ThrowsGraphException_WithoutTheBody_WhenGraphRefuses()
    {
        var handler = StubHttpMessageHandler.Json("""{"error":{"message":"secret detail"}}""", HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<GraphException>(
            () => CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None));

        Assert.Contains("403", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindUser_WrapsTransportFailures()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network"));

        var exception = await Assert.ThrowsAsync<GraphException>(
            () => CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task FindUser_WrapsUnreadableResponses()
    {
        var handler = StubHttpMessageHandler.Json("not json");

        await Assert.ThrowsAsync<GraphException>(
            () => CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", CancellationToken.None));
    }

    [Fact]
    public async Task FindUser_DoesNotWrapCancellationRequestedByTheCaller()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).FindUserBySamAccountNameAsync("jsmith", cancellation.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task FindUser_RejectsABlankName(string name) =>
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateClient(StubHttpMessageHandler.Json("{}")).FindUserBySamAccountNameAsync(name, CancellationToken.None));

    [Fact]
    public async Task DisabledClient_FindsNobody() =>
        Assert.Null(await new DisabledGraphClient().FindUserBySamAccountNameAsync("jsmith", CancellationToken.None));
}
