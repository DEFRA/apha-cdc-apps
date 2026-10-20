using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Microsoft.Extensions.Options;

namespace CDC.Api.Infrastructure.Email;

/// <summary>Raised when Microsoft Graph rejects a send. Carries the status and Graph error code only - no message content.</summary>
public sealed class ReviewEmailSendException(HttpStatusCode statusCode, string? graphErrorCode)
    : Exception($"Microsoft Graph sendMail failed with status {(int)statusCode} ({graphErrorCode ?? "no error code"}).")
{
    /// <summary>The HTTP status returned by Graph.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>The Graph error code, for example <c>ErrorAccessDenied</c>.</summary>
    public string? GraphErrorCode { get; } = graphErrorCode;
}

/// <summary>
/// Sends mail through the Microsoft Graph <c>sendMail</c> endpoint as <see cref="GraphMailOptions.SenderAddress"/>.
/// Retries throttling (429) and transient (503/504) responses a small number of times.
/// </summary>
public sealed class GraphReviewEmailSender(
    HttpClient httpClient,
    IGraphTokenProvider tokenProvider,
    IOptions<GraphMailOptions> options,
    ILogger<GraphReviewEmailSender> logger) : IReviewEmailSender
{
    internal const int MaxAttempts = 3;
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public void ValidateConfiguration() => options.Value.Validate();

    /// <inheritdoc />
    public async Task SendAsync(ReviewEmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        settings.Validate();

        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var url = $"users/{Uri.EscapeDataString(settings.SenderAddress!)}/sendMail";
        var payload = new
        {
            message = new
            {
                subject = message.Subject,
                body = new { contentType = "HTML", content = message.HtmlBody },
                toRecipients = new[] { new { emailAddress = new { address = message.ToAddress, name = message.ToName } } }
            },
            saveToSentItems = false
        };

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (attempt < MaxAttempts && IsTransient(response.StatusCode))
            {
                var delay = RetryDelay(response);
                logger.GraphRetrying((int)response.StatusCode, delay.TotalSeconds, attempt);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            throw new ReviewEmailSendException(response.StatusCode, await ReadErrorCodeAsync(response, cancellationToken));
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;

        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

            return document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
