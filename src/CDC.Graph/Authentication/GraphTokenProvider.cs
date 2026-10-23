using Azure.Core;

namespace CDC.Graph.Authentication;

/// <summary>
/// Default <see cref="IGraphTokenProvider"/>. The credential (a singleton) caches the token in memory and
/// requests a new one shortly before it expires, so callers can ask on every request.
/// </summary>
/// <param name="credential">The app's client-credentials identity.</param>
internal sealed class GraphTokenProvider(TokenCredential credential) : IGraphTokenProvider
{
    /// <summary>"Every Graph permission this app has been granted" - the only scope valid for client credentials.</summary>
    internal const string Scope = "https://graph.microsoft.com/.default";

    private static readonly TokenRequestContext Context = new([Scope]);

    /// <inheritdoc />
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(Context, cancellationToken);

        return token.Token;
    }
}
