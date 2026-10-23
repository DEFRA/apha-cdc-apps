using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using CDC.Graph.Authentication;

namespace CDC.Graph.Tests;

public class GraphAuthenticationTests
{
    private sealed class FakeCredential(string token) : TokenCredential
    {
        public int Calls { get; private set; }

        public string[] LastScopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            Issue(requestContext);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Issue(requestContext));

        private AccessToken Issue(TokenRequestContext requestContext)
        {
            Calls++;
            LastScopes = requestContext.Scopes;

            return new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
        }
    }

    private sealed class FakeTokenProvider(string token) : IGraphTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult(token);
    }

    [Fact]
    public async Task TokenProvider_RequestsTheGraphDefaultScope_AndReturnsTheToken()
    {
        var credential = new FakeCredential("the-token");

        var token = await new GraphTokenProvider(credential).GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("the-token", token);
        Assert.Equal(["https://graph.microsoft.com/.default"], credential.LastScopes);
    }

    [Fact]
    public async Task AuthorizationHandler_AddsTheBearerToken_ToEveryRequest()
    {
        var inner = StubHttpMessageHandler.Json("{}");
        using var client = new HttpClient(new GraphAuthorizationHandler(new FakeTokenProvider("abc")) { InnerHandler = inner });

        using var first = await client.GetAsync(new Uri("https://graph.example/one"));
        using var second = await client.GetAsync(new Uri("https://graph.example/two"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.All(inner.Requests, request => Assert.Equal(new AuthenticationHeaderValue("Bearer", "abc"), request.Headers.Authorization));
        Assert.Equal(2, inner.Requests.Count);
    }
}
