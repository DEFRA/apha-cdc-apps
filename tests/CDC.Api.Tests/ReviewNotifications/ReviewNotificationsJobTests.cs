using System.Data;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Infrastructure;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewNotificationsJobTests : IDisposable
{
    private const string DatabaseTimeQuery = "SELECT GETUTCDATE()";

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ReviewNotificationsJob>> logger = new();

    public ReviewNotificationsJobTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ReviewNotificationsJob CreateJob() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task RunAsync_ReturnsTrue_WhenDatabaseConnectionSucceeds()
    {
        connection.Script(DatabaseTimeQuery, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["DatabaseTimeUtc"], [[new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)]])]
        });

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeTrue();
        VerifyErrorLogged(Times.Never());
    }

    [Fact]
    public async Task RunAsync_ExecutesExpectedQuery_WhenDatabaseConnectionSucceeds()
    {
        connection.Script(DatabaseTimeQuery, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["DatabaseTimeUtc"], [[new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)]])]
        });

        await CreateJob().RunAsync(CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(DatabaseTimeQuery);
        executed.CommandType.Should().Be(CommandType.Text);
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_AndLogsError_WhenDatabaseConnectionFails()
    {
        connection.Script(DatabaseTimeQuery, new FakeCommandScript
        {
            Throws = new InvalidOperationException("simulated database failure")
        });

        var result = await CreateJob().RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        VerifyErrorLogged(Times.Once());
    }

    [Fact]
    public async Task RunAsync_ReturnsFalse_WhenConnectionFactoryThrows()
    {
        var job = new ReviewNotificationsJob(
            new ThrowingConnectionFactory(() => throw new InvalidOperationException("Database:Host is not configured")),
            logger.Object);

        var result = await job.RunAsync(CancellationToken.None);

        result.Should().BeFalse();
        VerifyErrorLogged(Times.Once());
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

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class ThrowingConnectionFactory(Func<IDbConnection> factory) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => factory();
    }
}
