using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CDC.Api.Tests.ExternalUsers;

public class ExternalUserLoginInstructionsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IExternalUsersRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IEmailSender> sender = new(MockBehavior.Strict);
    private readonly ReviewNotificationsOptions reviewOptions = new();
    private readonly AppOptions appOptions = new() { BaseUrl = "https://d2r2.example.test/" };
    private readonly Mock<ILogger<ExternalUserLoginInstructionsService>> logger = new();

    private readonly ExternalUserLoginRecipient recipient = new(Guid.NewGuid(), "Jo Smith", "jo@example.test", LoginInstructionsSent: false);

    public ExternalUserLoginInstructionsServiceTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private ExternalUserLoginInstructionsService CreateService() => new(
        repository.Object,
        sender.Object,
        Options.Create(reviewOptions),
        Options.Create(appOptions),
        new FixedTimeProvider(Now),
        logger.Object);

    private void GivenTheRecipient(ExternalUserLoginRecipient? found) =>
        repository.Setup(repo => repo.GetLoginRecipientAsync(recipient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(found);

    private void GivenTheEmailIsAccepted() =>
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

    private void GivenTheSentFlagIsRecorded(bool recorded) =>
        repository
            .Setup(repo => repo.MarkLoginInstructionsSentAsync(recipient.Id, recipient.EmailAddress, Now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recorded);

    private Task<Result<Unit>> SendAsync(CancellationToken cancellationToken = default) =>
        CreateService().SendAsync(new SendExternalUserLoginInstructionsCommand(recipient.Id), cancellationToken);

    [Fact]
    public async Task SendAsync_EmailsTheSavedAddress_ThenMarksTheUserAsSent()
    {
        GivenTheRecipient(recipient);
        EmailMessage? sent = null;
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.CompletedTask);
        GivenTheSentFlagIsRecorded(true);

        var result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        sent!.ToAddress.Should().Be("jo@example.test");
        sent.TemplateId.Should().Be(ExternalUserLoginInstructionsComposer.TemplateId);
        sent.Personalisation.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["name"] = "Jo Smith", ["AppUrl"] = "https://d2r2.example.test/Account/Login" });
        repository.Verify(repo => repo.MarkLoginInstructionsSentAsync(recipient.Id, "jo@example.test", Now.UtcDateTime, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SendsToTheTestRecipientInstead_WhenOneIsConfigured_ButRecordsTheUsersOwnAddress()
    {
        reviewOptions.TestRecipient = "tester@example.test";
        GivenTheRecipient(recipient);
        EmailMessage? sent = null;
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.CompletedTask);
        GivenTheSentFlagIsRecorded(true);

        var result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        sent!.ToAddress.Should().Be("tester@example.test");
    }

    [Fact]
    public async Task SendAsync_ReturnsNotFound_WhenTheUserIsNotAnExternalUser()
    {
        GivenTheRecipient(null);

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.NotFound);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_ReturnsConflict_WithoutEmailing_WhenTheInstructionsWereAlreadySent()
    {
        GivenTheRecipient(recipient with { LoginInstructionsSent = true });

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.Conflict);
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_ReturnsValidationFailed_WithoutEmailing_WhenThereIsNoAddress(string? emailAddress)
    {
        GivenTheRecipient(recipient with { EmailAddress = emailAddress! });

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_ReturnsDependencyFailed_WithoutEmailing_WhenThereIsNoLinkToGive()
    {
        appOptions.BaseUrl = string.Empty;
        GivenTheRecipient(recipient);

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.DependencyFailed);
        sender.VerifyNoOtherCalls();
        repository.Verify(repo => repo.MarkLoginInstructionsSentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task SendAsync_ReturnsDependencyFailed_AndLeavesTheUserUnsent_WhenTheEmailFails(Type exceptionType)
    {
        GivenTheRecipient(recipient);
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(exceptionType, "Notify rejected the email: 400")!);

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.DependencyFailed);
        result.Error.Should().NotContain("Notify").And.NotContain("jo@example.test");
        repository.Verify(repo => repo.MarkLoginInstructionsSentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_PropagatesCancellationOfTheSend()
    {
        GivenTheRecipient(recipient);
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var act = () => SendAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_ReturnsConflict_WhenTheAddressChangedDuringTheSend()
    {
        GivenTheRecipient(recipient);
        GivenTheEmailIsAccepted();
        GivenTheSentFlagIsRecorded(false);

        var result = await SendAsync();

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Contain("changed");
    }

    [Fact]
    public async Task SendAsync_RecordsTheSentFlagEvenWhenTheCallerHasCancelled()
    {
        GivenTheRecipient(recipient);
        using var cancellation = new CancellationTokenSource();
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .Returns(Task.CompletedTask);
        GivenTheSentFlagIsRecorded(true);

        var result = await SendAsync(cancellation.Token);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.MarkLoginInstructionsSentAsync(recipient.Id, recipient.EmailAddress, Now.UtcDateTime, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SendAsync_NeverLogsTheEmailAddress()
    {
        GivenTheRecipient(recipient);
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

        await SendAsync();

        logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2]?.ToString())
            .Should().NotContain(state => state!.Contains("jo@example.test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_ThrowsArgumentNullException_WhenTheCommandIsNull()
    {
        var act = () => CreateService().SendAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
