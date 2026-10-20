using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewNotificationsJobTests
{
    private readonly Mock<IReviewNotificationRepository> repository = new();
    private readonly Mock<IReviewEmailSender> sender = new();
    private readonly Mock<ILogger<ReviewNotificationsJob>> logger = new();
    private readonly GraphMailOptions mailOptions = new() { Enabled = true };

    public ReviewNotificationsJobTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private ReviewNotificationsJob CreateJob() => new(
        repository.Object,
        sender.Object,
        Options.Create(mailOptions),
        Options.Create(new ReviewNotificationsOptions()),
        new FixedTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero)),
        logger.Object);

    private static ReviewNotificationRecipient Recipient(bool withSections = true) => new(
        Guid.NewGuid(),
        "Test User",
        "test.user@example.com",
        [new ReviewProfileVersion(
            Guid.NewGuid(),
            "Profile",
            null,
            "1.0",
            withSections
                ? [new ReviewSection(1, "Introduction", new ReviewSchedule(null, null), new ReviewSchedule(null, null), new ReviewSchedule(null, null))]
                : [])]);

    [Fact]
    public async Task RunAsync_SendsOneEmailPerRecipientWithSections_AndReturnsTrue()
    {
        repository.Setup(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient(), Recipient(), Recipient(withSections: false)]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        sender.Verify(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        VerifyLogged(LogLevel.Error, Times.Never());
    }

    [Fact]
    public async Task RunAsync_SendsNothing_WhenNobodyIsDue()
    {
        repository.Setup(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        sender.Verify(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task RunAsync_ReturnsTrueWithoutReadingUsers_WhenDisabled()
    {
        mailOptions.Enabled = false;

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        repository.Verify(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()), Times.Never());
        VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_WhenMailSettingsAreInvalid()
    {
        sender.Setup(s => s.ValidateConfiguration()).Throws(new InvalidOperationException("missing"));

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        repository.Verify(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()), Times.Never());
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_WhenReadingUsersFails()
    {
        repository.Setup(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never());
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task RunAsync_KeepsSendingAndReturnsFalse_WhenOneEmailFails()
    {
        repository.Setup(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient(), Recipient()]);
        sender.SetupSequence(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rejected"))
            .Returns(Task.CompletedTask);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_WhenUnexpectedErrorOccurs()
    {
        repository.Setup(r => r.GetRecipientsDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient()]);
        sender.Setup(s => s.SendAsync(It.IsAny<ReviewEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1873", Justification = "Mock verification expression, not a log call.")]
    private void VerifyLogged(LogLevel level, Times times) =>
        logger.Verify(
            log => log.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
