using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Sends mail through the Microsoft Graph <c>sendMail</c> endpoint using the OAuth2 client
/// credentials flow. Needs the <c>Mail.Send</c> application permission.
/// </summary>
/// <param name="httpClient">Client used for the token and Graph calls.</param>
/// <param name="options">Graph mail settings.</param>
public sealed class GraphEmailSender(HttpClient httpClient, IOptions<GraphMailOptions> options) : IEmailSender
{
    private const string GraphScope = "https://graph.microsoft.com/.default";

    private readonly GraphMailOptions settings = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Email sending is disabled: SmtpSettings:Enabled is false.");
        }

        var token = await GetAccessTokenAsync(cancellationToken);

        var payload = new
        {
            message = new
            {
                subject = message.Subject,
                body = new { contentType = "HTML", content = message.HtmlBody },
                toRecipients = new[] { new { emailAddress = new { address = message.ToAddress, name = message.ToName } } }
            },
            saveToSentItems = true
        };

        var url = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(settings.SenderAddress)}/sendMail";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(settings.TenantId)}/oauth2/v2.0/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["scope"] = GraphScope
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        return string.IsNullOrEmpty(token?.AccessToken)
            ? throw new InvalidOperationException("The token endpoint returned no access token.")
            : token.AccessToken;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
