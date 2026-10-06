using System.Data;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewNotificationsJobTests : IDisposable
{
    private const string RecipientQuery =
        "SELECT [Id], [UserName], [EmailAddress], [FullName] FROM [dbo].[vwReviewEmailUsers]";

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ReviewNotificationsJob>> logger = new();

    public ReviewNotificationsJobTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RunAsync_ReturnsTrue_WhenTheRecipientsAreResolved()
    {
        var repository = new Mock<IReviewNotificationRepository>();
        repository
            .Setup(repo => repo.GetRecipientsDueReviewEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReviewEmailRecipient { Id = Guid.NewGuid() }]);

        var result = await new ReviewNotificationsJob(repository.Object, logger.Object).RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_AndLogsError_WhenTheRecipientQueryFails()
    {
        var repository = new Mock<IReviewNotificationRepository>();
        repository
            .Setup(repo => repo.GetRecipientsDueReviewEmailAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated database failure"));

        var result = await new ReviewNotificationsJob(repository.Object, logger.Object).RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task Repository_ReadsTheSubscriptionFilteredView()
    {
        var userId = Guid.NewGuid();
        connection.Script(RecipientQuery, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "EmailAddress", "FullName"],
                    [[userId, "jbloggs", "joe.bloggs@example.gov.uk", "Joe Bloggs"]])
            ]
        });

        var recipients = await CreateRepository().GetRecipientsDueReviewEmailAsync(CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(RecipientQuery);
        executed.CommandType.Should().Be(CommandType.Text);

        var recipient = recipients.Should().ContainSingle().Subject;
        recipient.Id.Should().Be(userId);
        recipient.EmailAddress.Should().Be("joe.bloggs@example.gov.uk");
    }

    [Fact]
    public async Task Repository_ReturnsNoRecipients_WhenEveryUserIsUnsubscribed()
    {
        connection.Script(RecipientQuery, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "UserName", "EmailAddress", "FullName"], [])]
        });

        var recipients = await CreateRepository().GetRecipientsDueReviewEmailAsync(CancellationToken.None);

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task Repository_Throws_WhenTheConnectionFactoryFails()
    {
        var repository = new ReviewNotificationRepository(
            new ThrowingConnectionFactory(() => throw new InvalidOperationException("Database:Host is not configured")),
            NullLogger<ReviewNotificationRepository>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.GetRecipientsDueReviewEmailAsync(CancellationToken.None));
    }

    private ReviewNotificationRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<ReviewNotificationRepository>.Instance);

    private void VerifyErrorLogged(Times times) =>
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class ThrowingConnectionFactory(Func<IDbConnection> factory) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => factory();
    }
}
