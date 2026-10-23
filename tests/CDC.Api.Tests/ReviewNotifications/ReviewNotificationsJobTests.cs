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

    private ReviewNotificationsOptions options = new()
    {
        ReviewerGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            [ReviewerGroups.Policy] = ["Policy reviewer"],
            [ReviewerGroups.AuthorTechnical] = ["Technical author"],
            [ReviewerGroups.VeterinaryAdvisor] = ["Veterinary advisor"]
        }
    };

    private ReviewNotificationsJob CreateJob() => new(
        repository.Object,
        sender.Object,
        Options.Create(options),
        TimeProvider.System,
        logger.Object);

    private static ReviewDueUser User(string name, string email, params string[] roles)
    {
        var due = new ReviewItem(DateTime.Today.AddDays(-1), "Active");
        var none = new ReviewItem(null, null);

        return new(
            Guid.NewGuid(),
            name,
            email,
            [new ReviewDueProfile(Guid.NewGuid(), "Anthrax", null, "1.0", [new ReviewDueSection(1, "Intro", due, due, due)], roles.Length == 0 ? ["Policy reviewer"] : roles)]);
    }

    [Fact]
    public async Task RunAsync_SendsOnlyReviewTypesOfTheUsersGroup()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test", "Technical author")]);
        EmailMessage? message = null;
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => message = m);

        await CreateJob().RunAsync(CancellationToken.None);

        // One due cell (Author) of the three types.
        message!.HtmlBody.Split("(Overdue)").Length.Should().Be(2);
    }

    [Theory]
    [InlineData("Policy reviewer")]
    [InlineData("Veterinary advisor")]
    public async Task RunAsync_SendsOnlyTheReviewTypeOfTheUsersGroup_ForPolicyAndVeterinaryAdvisor(string role)
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test", role)]);
        EmailMessage? message = null;
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => message = m);

        await CreateJob().RunAsync(CancellationToken.None);

        message!.ToAddress.Should().Be("ann@example.test");
        message.HtmlBody.Split("(Overdue)").Length.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_StillEmailsConfiguredGroups_AndFails_WhenOneGroupUnconfigured()
    {
        options.ReviewerGroups.Remove(ReviewerGroups.VeterinaryAdvisor);
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test", "Veterinary advisor"), User("Bob", "bob@example.test", "Policy reviewer")]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "ann@example.test"), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.SendAsync(It.Is<EmailMessage>(m => m.ToAddress == "bob@example.test"), It.IsAny<CancellationToken>()), Times.Once);
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task RunAsync_LogsReminderSentAndCompletion_WhenProcessingSucceeds()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test")]);

        await CreateJob().RunAsync(CancellationToken.None);

        VerifyLogged(LogLevel.Information, Times.Exactly(3));
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task RunAsync_SkipsUser_WhenNotInAnyReviewerGroup()
    {
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test", "Policy author")]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DoesNotSendAndLogs_WhenReviewerGroupNotConfigured()
    {
        options = new();
        repository.Setup(r => r.GetUsersDueReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("Ann", "ann@example.test")]);

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyErrorLogged(Times.Exactly(3));
    }

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

    private void VerifyErrorLogged(Times times) => VerifyLogged(LogLevel.Error, times);

    private void VerifyLogged(LogLevel level, Times times) =>
        logger.Verify(
            log => log.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
