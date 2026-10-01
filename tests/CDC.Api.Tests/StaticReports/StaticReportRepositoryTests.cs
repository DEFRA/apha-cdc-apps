using System.Data.Common;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportRepositoryTests : IDisposable
{
    private static readonly Guid VersionId = Guid.Parse("c80b8e93-21d2-453a-b0e0-3f522d03971d");
    private static readonly Guid StaticReportId = Guid.Parse("65fe96f2-ea67-4618-9e45-0af0026da1ef");

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<StaticReportRepository>> logger = new();

    public StaticReportRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private StaticReportRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetCurrentAsync_MapsRowsAndPassesParameters()
    {
        connection.Script(StaticReportStoredProcedures.GetCurrent, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "StaticReportId", "Title", "VersionMajor", "EffectiveDateFrom", "EffectiveDateTo", "IsUserManual", "IsPublic", "FileSize"],
                    [[VersionId, StaticReportId, "D2R2 Quality Statement", (byte)1, new DateTime(2024, 4, 26), null, true, false, 225133]])
            ]
        });

        var reports = await CreateRepository().GetCurrentAsync(isUserManual: true, CancellationToken.None);

        reports.Should().ContainSingle();
        reports[0].Title.Should().Be("D2R2 Quality Statement");
        reports[0].IsCurrent.Should().BeTrue();
        reports[0].FileSize.Should().Be(225133);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("IsUserManual").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetCurrentAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetCurrent, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCurrentAsync(isUserManual: false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetDataAsync_ReturnsNull_WhenNoVersionExists()
    {
        connection.Script(StaticReportStoredProcedures.GetData, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("PdfData", "IsPublic", "Title")]
        });

        var data = await CreateRepository().GetDataAsync(VersionId, CancellationToken.None);

        data.Should().BeNull();
    }

    [Fact]
    public async Task GetDataAsync_ReturnsPersistedBytes()
    {
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46];

        connection.Script(StaticReportStoredProcedures.GetData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PdfData", "IsPublic", "Title"], [[pdfBytes, false, "D2R2 Quality Statement"]])]
        });

        var data = await CreateRepository().GetDataAsync(VersionId, CancellationToken.None);

        data.Should().NotBeNull();
        data!.PdfData.Should().Equal(pdfBytes);
        data.Title.Should().Be("D2R2 Quality Statement");
    }

    [Fact]
    public async Task GetDataAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetDataAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UploadAsync_PassesParameters()
    {
        connection.Script(StaticReportStoredProcedures.Upload, new FakeCommandScript());
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46];

        await CreateRepository().UploadAsync("D2R2 Quality Statement", pdfBytes, isUserManual: true, isPublic: false, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("Title").WhoseValue.Should().Be("D2R2 Quality Statement");
        executed.Parameters.Should().ContainKey("IsUserManual").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task UploadAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.Upload, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UploadAsync("D2R2 Quality Statement", [1, 2, 3], false, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetDataAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new StaticReportRepository(new StubConnectionFactory(new NotADbConnection()), logger.Object);

        var act = () => repository.GetDataAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDataAsync_RethrowsAndLogs_WhenOpenAsyncFails()
    {
        var repository = new StaticReportRepository(new StubConnectionFactory(new ThrowingOpenDbConnection()), logger.Object);

        var act = () => repository.GetDataAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(System.Data.IDbConnection connection) : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => connection;
    }

    /// <summary>A bare <see cref="System.Data.IDbConnection"/> (not a <see cref="DbConnection"/>) to exercise the repository's defensive type check.</summary>
    private sealed class NotADbConnection : System.Data.IDbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string ConnectionString { get; set; } = string.Empty;

        public int ConnectionTimeout => 0;

        public string Database => string.Empty;

        public System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public System.Data.IDbTransaction BeginTransaction() => throw new NotSupportedException();

        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();

        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public void Close()
        {
        }

        public System.Data.IDbCommand CreateCommand() => throw new NotSupportedException();

        public void Open()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A <see cref="DbConnection"/> whose <see cref="Open"/> always fails, to exercise the open-connection failure path.</summary>
    private sealed class ThrowingOpenDbConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open() => throw new FakeDbException("connect failed");

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
