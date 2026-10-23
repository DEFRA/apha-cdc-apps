using System.Net.Http.Headers;

namespace CDC.Graph.Authentication;

/// <summary>Adds the Graph bearer token to every outgoing request, fetching it per attempt so a retry never reuses an expired one.</summary>
/// <param name="tokenProvider">Supplies the cached access token.</param>
internal sealed class GraphAuthorizationHandler(IGraphTokenProvider tokenProvider) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
