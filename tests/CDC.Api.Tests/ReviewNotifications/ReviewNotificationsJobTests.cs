using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewNotificationsJobTests
{
    private readonly Mock<IReviewNotificationsRepository> repository = new();
    private readonly Mock<IEmailSender> sender = new();
    private readonly Mock<ILogger<ReviewNotificationsJob>> logger = new();

    public ReviewNotificationsJobTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private ReviewNotificationsJob CreateJob() => new(
        repository.Object,
        sender.Object,
        Options.Create(new ReviewNotificationsOptions()),
        TimeProvider.System,
        logger.Object);

    private static ReviewDueUser User(string name, string email) => new(
        Guid.NewGuid(),
        name,
        email,
        [new ReviewDueProfile(Guid.NewGuid(), "Anthrax", null, "1.0", [])]);

    [Fact]
    public async Task RunAsync_SendsNothing_WhenNoReviewsDue()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_SendsOneEmailPerDueUser()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test"), User("Bob", "bob@example.test")]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        sender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "ann@example.test" && m.Subject == ReviewEmailComposer.Subject), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "bob@example.test"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ContinuesAndReturnsFalse_WhenOneSendFails()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test"), User("Bob", "bob@example.test")]);
        sender.Setup(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "ann@example.test"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "bob@example.test"), It.IsAny<CancellationToken>()), Times.Once);
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_AndSendsNothing_WhenLookupFails()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task RunAsync_Propagates_WhenCancelled()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test")]);
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => CreateJob().RunAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private void VerifyErrorLogged(Times times) =>
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
