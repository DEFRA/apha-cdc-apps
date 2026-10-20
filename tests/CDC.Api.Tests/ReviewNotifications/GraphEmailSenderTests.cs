using System.Net;
using System.Text.Json;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CDC.Api.Tests.ReviewNotifications;

public class GraphEmailSenderTests
{
    private static readonly EmailMessage Message = new("to@example.test", "To Person", "Subject", "<p>Hi</p>");

    private static GraphMailOptions Settings(bool enabled = true) => new()
    {
        Enabled = enabled,
        TenantId = "tenant",
        ClientId = "client",
        ClientSecret = "secret",
        SenderAddress = "sender@example.test"
    };

    private static GraphEmailSender CreateSender(RecordingHandler handler, GraphMailOptions settings) =>
        new(new HttpClient(handler), Options.Create(settings));

    [Fact]
    public async Task SendAsync_AcquiresTokenThenPostsToSendMail()
    {
        var handler = new RecordingHandler();

        await CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Uri.Should().Be("https://login.microsoftonline.com/tenant/oauth2/v2.0/token");
        handler.Requests[0].Body.Should().Contain("client_credentials").And.Contain("client_id=client");
        handler.Requests[1].Uri.Should().Be("https://graph.microsoft.com/v1.0/users/sender%40example.test/sendMail");
        handler.Requests[1].Authorization.Should().Be("Bearer token-value");

        using var json = JsonDocument.Parse(handler.Requests[1].Body);
        var graphMessage = json.RootElement.GetProperty("message");
        graphMessage.GetProperty("subject").GetString().Should().Be("Subject");
        graphMessage.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
        graphMessage.GetProperty("toRecipients")[0].GetProperty("emailAddress").GetProperty("address").GetString().Should().Be("to@example.test");
    }

    [Fact]
    public async Task SendAsync_Throws_WhenDisabled()
    {
        var handler = new RecordingHandler();

        var act = () => CreateSender(handler, Settings(enabled: false)).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenTokenEndpointFails()
    {
        var handler = new RecordingHandler { TokenStatus = HttpStatusCode.Unauthorized };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenTokenMissingFromResponse()
    {
        var handler = new RecordingHandler { TokenBody = "{}" };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenGraphRejectsTheMessage()
    {
        var handler = new RecordingHandler { GraphStatus = HttpStatusCode.Forbidden };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed record RecordedRequest(string Uri, string Body, string? Authorization);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public HttpStatusCode TokenStatus { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode GraphStatus { get; init; } = HttpStatusCode.Accepted;

        public string TokenBody { get; init; } = "{\"access_token\":\"token-value\"}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.RequestUri!.OriginalString, body, request.Headers.Authorization?.ToString()));

            return request.RequestUri.Host.Contains("login.microsoftonline.com", StringComparison.Ordinal)
                ? new HttpResponseMessage(TokenStatus) { Content = new StringContent(TokenBody) }
                : new HttpResponseMessage(GraphStatus);
        }
    }
}
