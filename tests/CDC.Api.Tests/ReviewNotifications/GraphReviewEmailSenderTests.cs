using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Api.Tests.ReviewNotifications;

public sealed class GraphReviewEmailSenderTests : IDisposable
{
    private static readonly ReviewEmailMessage Message = new("to@example.com", "To Name", "Subject line", "<p>Hello</p>");

    private readonly QueueHandler handler = new();
    private readonly GraphMailOptions options = new()
    {
        TenantId = "tenant",
        ClientId = "client",
        ClientSecret = "secret",
        SenderAddress = "no-reply@example.com"
    };

    public void Dispose() => handler.Dispose();

    private GraphReviewEmailSender CreateSender() => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://graph.example/v1.0/") },
        new StubTokenProvider(),
        Options.Create(options),
        NullLogger<GraphReviewEmailSender>.Instance);

    [Fact]
    public async Task SendAsync_PostsExpectedGraphRequest()
    {
        handler.Enqueue(HttpStatusCode.Accepted);

        await CreateSender().SendAsync(Message, CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be("https://graph.example/v1.0/users/no-reply%40example.com/sendMail");
        request.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "token-123"));

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        root.GetProperty("saveToSentItems").GetBoolean().Should().BeFalse();
        var message = root.GetProperty("message");
        message.GetProperty("subject").GetString().Should().Be("Subject line");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
        message.GetProperty("body").GetProperty("content").GetString().Should().Be("<p>Hello</p>");
        var address = message.GetProperty("toRecipients")[0].GetProperty("emailAddress");
        address.GetProperty("address").GetString().Should().Be("to@example.com");
        address.GetProperty("name").GetString().Should().Be("To Name");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendAsync_RetriesTransientFailures(HttpStatusCode transient)
    {
        handler.Enqueue(transient, retryAfterSeconds: 0);
        handler.Enqueue(HttpStatusCode.Accepted);

        await CreateSender().SendAsync(Message, CancellationToken.None);

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendAsync_ThrowsAfterMaxAttempts_WhenStillThrottled()
    {
        for (var i = 0; i < GraphReviewEmailSender.MaxAttempts; i++)
        {
            handler.Enqueue(HttpStatusCode.TooManyRequests, retryAfterSeconds: 0);
        }

        var act = () => CreateSender().SendAsync(Message, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<ReviewEmailSendException>()).Which;
        exception.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        handler.Requests.Should().HaveCount(GraphReviewEmailSender.MaxAttempts);
    }

    [Fact]
    public async Task SendAsync_ThrowsWithGraphErrorCode_AndDoesNotRetry_WhenRejected()
    {
        handler.Enqueue(HttpStatusCode.Forbidden, body: "{\"error\":{\"code\":\"ErrorAccessDenied\",\"message\":\"secret detail\"}}");

        var act = () => CreateSender().SendAsync(Message, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<ReviewEmailSendException>()).Which;
        exception.GraphErrorCode.Should().Be("ErrorAccessDenied");
        exception.Message.Should().Contain("403").And.Contain("ErrorAccessDenied").And.NotContain("secret detail");
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"other\":1}")]
    [InlineData("{\"error\":{}}")]
    public async Task SendAsync_ThrowsWithoutErrorCode_WhenBodyHasNone(string body)
    {
        handler.Enqueue(HttpStatusCode.BadRequest, body: body);

        var act = () => CreateSender().SendAsync(Message, CancellationToken.None);

        (await act.Should().ThrowAsync<ReviewEmailSendException>()).Which.GraphErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_Throws_WhenSettingsAreIncomplete()
    {
        options.ClientSecret = null;

        var act = () => CreateSender().SendAsync(Message, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SmtpSettings:ClientSecret*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_NamesEveryMissingSetting()
    {
        var empty = new GraphReviewEmailSender(
            new HttpClient(handler),
            new StubTokenProvider(),
            Options.Create(new GraphMailOptions()),
            NullLogger<GraphReviewEmailSender>.Instance);

        var act = empty.ValidateConfiguration;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TenantId*ClientId*ClientSecret*SenderAddress*");
    }

    [Fact]
    public void ValidateConfiguration_Passes_WhenComplete() =>
        CreateSender().Invoking(s => s.ValidateConfiguration()).Should().NotThrow();

    [Fact]
    public void CreateCredential_Throws_WhenSettingsAreIncomplete()
    {
        var act = () => ClientCredentialsTokenProvider.CreateCredential(new GraphMailOptions());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateCredential_ReturnsCredential_WhenSettingsAreComplete() =>
        ClientCredentialsTokenProvider.CreateCredential(options).Should().NotBeNull();

    [Fact]
    public async Task GetAccessTokenAsync_Throws_WhenSettingsAreIncomplete()
    {
        var provider = new ClientCredentialsTokenProvider(Options.Create(new GraphMailOptions()));

        var act = () => provider.GetAccessTokenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubTokenProvider : IGraphTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("token-123");
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, AuthenticationHeaderValue? Authorization, string Body);

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public List<CapturedRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, int? retryAfterSeconds = null, string body = "")
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfterSeconds is { } seconds)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            }

            responses.Enqueue(response);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            return responses.Dequeue();
        }
    }
}
