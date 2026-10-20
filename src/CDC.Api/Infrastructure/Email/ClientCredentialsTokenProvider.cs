using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace CDC.Api.Infrastructure.Email;

/// <summary>Supplies Microsoft Graph access tokens.</summary>
public interface IGraphTokenProvider
{
    /// <summary>Gets a token for the <c>https://graph.microsoft.com/.default</c> scope.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>Acquires Graph tokens with the OAuth2 client-credentials flow; the credential is built on first use.</summary>
public sealed class ClientCredentialsTokenProvider(IOptions<GraphMailOptions> options) : IGraphTokenProvider
{
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    private TokenCredential? credential;

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        credential ??= CreateCredential(options.Value);
        var token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);

        return token.Token;
    }

    internal static ClientSecretCredential CreateCredential(GraphMailOptions settings)
    {
        settings.Validate();

        return new ClientSecretCredential(settings.TenantId, settings.ClientId, settings.ClientSecret);
    }
}
