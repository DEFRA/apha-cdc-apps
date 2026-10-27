using System.Net;
using System.Text;
using System.Text.Json;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CDC.Api.Tests.ReviewNotifications;

public class NotifyEmailSenderTests
{
    private const string ServiceId = "11111111-1111-1111-1111-111111111111";
    private const string Secret = "22222222-2222-2222-2222-222222222222";

    private static readonly EmailMessage Message = new("to@example.test", "To Person", "Subject", "Body text");

    private static NotifyOptions Settings(bool enabled = true, string? apiKey = null) => new()
    {
        Enabled = enabled,
        ApiKey = apiKey ?? $"d2r2-{ServiceId}-{Secret}",
        TemplateId = "template-id"
    };

    private static NotifyEmailSender CreateSender(RecordingHandler handler, NotifyOptions settings) =>
        new(new HttpClient(handler), Options.Create(settings), TimeProvider.System);

    [Fact]
    public async Task SendAsync_PostsTemplateAndPersonalisationWithSignedToken()
    {
        var handler = new RecordingHandler();

        await CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        handler.Uri.Should().Be("https://api.notifications.service.gov.uk/v2/notifications/email");

        using var json = JsonDocument.Parse(handler.Body);
        json.RootElement.GetProperty("email_address").GetString().Should().Be("to@example.test");
        json.RootElement.GetProperty("template_id").GetString().Should().Be("template-id");
        var personalisation = json.RootElement.GetProperty("personalisation");
        personalisation.GetProperty("name").GetString().Should().Be("To Person");
        personalisation.GetProperty("subject").GetString().Should().Be("Subject");
        personalisation.GetProperty("body").GetString().Should().Be("Body text");

        var parts = handler.Token!.Split('.');
        parts.Should().HaveCount(3);
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        claims.RootElement.GetProperty("iss").GetString().Should().Be(ServiceId);
        claims.RootElement.GetProperty("iat").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendAsync_AddsFileLinkAndUsesTheAttachmentTemplate_WhenAttachmentGiven()
    {
        var handler = new RecordingHandler();
        var settings = Settings();
        settings.AttachmentTemplateId = "attachment-template";
        var message = Message with { Attachment = new EmailAttachment("c.pdf", [1, 2, 3]) };

        await CreateSender(handler, settings).SendAsync(message, CancellationToken.None);

        using var json = JsonDocument.Parse(handler.Body);
        json.RootElement.GetProperty("template_id").GetString().Should().Be("attachment-template");
        var file = json.RootElement.GetProperty("personalisation").GetProperty("link_to_file");
        file.GetProperty("file").GetString().Should().Be(Convert.ToBase64String([1, 2, 3]));
        file.GetProperty("filename").GetString().Should().Be("c.pdf");
    }

    [Fact]
    public async Task SendAsync_IgnoresTheAttachment_WhenNoAttachmentTemplateIsConfigured()
    {
        var handler = new RecordingHandler();
        var message = Message with { Attachment = new EmailAttachment("c.pdf", [1]) };

        await CreateSender(handler, Settings()).SendAsync(message, CancellationToken.None);

        using var json = JsonDocument.Parse(handler.Body);
        json.RootElement.GetProperty("template_id").GetString().Should().Be("template-id");
        json.RootElement.GetProperty("personalisation").TryGetProperty("link_to_file", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenDisabled()
    {
        var handler = new RecordingHandler();

        var act = () => CreateSender(handler, Settings(enabled: false)).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Uri.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenApiKeyMalformed()
    {
        var act = () => CreateSender(new RecordingHandler(), Settings(apiKey: "short")).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenNotifyRejectsTheMessage()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.BadRequest };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_IncludesNotifyErrorText_WhenRejected()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.BadRequest, ResponseBody = "{\"errors\":[{\"error\":\"BadRequestError\",\"message\":\"Missing personalisation: body\"}]}" };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.Message.Should().Contain("400").And.Contain("BadRequestError: Missing personalisation: body");
    }

    [Fact]
    public async Task SendAsync_Throws_WhenNotifyErrorBodyIsNotJson()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.InternalServerError, ResponseBody = "oops" };

        var act = () => CreateSender(handler, Settings()).SendAsync(Message, CancellationToken.None);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Contain("500");
    }

    private static string Decode(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Uri { get; private set; }

        public string Body { get; private set; } = string.Empty;

        public string? Token { get; private set; }

        public HttpStatusCode Status { get; init; } = HttpStatusCode.Created;

        public string ResponseBody { get; init; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri!.OriginalString;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Token = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody) };
        }
    }
}
