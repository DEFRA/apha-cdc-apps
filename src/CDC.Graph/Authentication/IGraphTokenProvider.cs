namespace CDC.Graph.Authentication;

/// <summary>Supplies the access token that authorises this application's own calls to Microsoft Graph.</summary>
public interface IGraphTokenProvider
{
    /// <summary>Gets a valid access token, reusing the cached one until shortly before it expires.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The bearer token.</returns>
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
