using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Sends mail through the GOV.UK Notify <c>v2/notifications/email</c> endpoint using a template
/// with <c>((subject))</c>, <c>((name))</c> and <c>((body))</c> personalisation.
/// </summary>
/// <param name="httpClient">Client used for the Notify call.</param>
/// <param name="options">Notify settings.</param>
/// <param name="timeProvider">Clock used for the API token issue time.</param>
public sealed class NotifyEmailSender(HttpClient httpClient, IOptions<NotifyOptions> options, TimeProvider timeProvider) : IEmailSender
{
    private const int GuidLength = 36;

    private readonly NotifyOptions settings = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Email sending is disabled: Notify:Enabled is false.");
        }

        var personalisation = message.Personalisation is { } templateValues
            ? templateValues.ToDictionary(pair => pair.Key, pair => (object)pair.Value)
            : new Dictionary<string, object>
            {
                ["name"] = message.ToName,
                ["subject"] = message.Subject,
                ["body"] = message.Body
            };

        var payload = new
        {
            email_address = message.ToAddress,
            template_id = message.TemplateId ?? settings.TemplateId,
            personalisation
        };

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v2/notifications/email";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorAsync(response, cancellationToken);
            throw new HttpRequestException($"Notify rejected the email: {(int)response.StatusCode} {response.ReasonPhrase}. {detail}".TrimEnd(), null, response.StatusCode);
        }
    }

    // Notify returns {"errors":[{"error":"...","message":"..."}]}; the messages never echo the recipient address.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<NotifyErrorResponse>(cancellationToken);

            return string.Join(" ", body?.Errors?.Select(e => $"{e.Error}: {e.Message}") ?? []);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record NotifyErrorResponse(List<NotifyError>? Errors);

    private sealed record NotifyError(string? Error, string? Message);

    // Notify keys are "{name}-{service id}-{secret}", the last two parts being GUIDs; the token is an HS256 JWT.
    private string CreateToken()
    {
        var key = settings.ApiKey;
        var minLength = (2 * GuidLength) + 1;

        if (key.Length < minLength)
        {
            throw new InvalidOperationException("The Notify API key is missing or malformed.");
        }

        var secret = key[^GuidLength..];
        var serviceId = key.Substring(key.Length - minLength, GuidLength);

        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "HS256" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = serviceId, iat = timeProvider.GetUtcNow().ToUnixTimeSeconds() }));
        var signingInput = string.Create(CultureInfo.InvariantCulture, $"{header}.{claims}");
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput));

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
