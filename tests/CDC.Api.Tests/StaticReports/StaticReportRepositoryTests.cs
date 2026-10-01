using CDC.Api.Domain.Exceptions;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportRepositoryTests : IDisposable
{
    private static readonly string[] VersionColumns =
    [
        "Id", "StaticReportId", "Title", "VersionMajor", "EffectiveDateFrom", "EffectiveDateTo", "IsUserManual", "IsPublic", "FileSize"
    ];

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
                new FakeResultSet(VersionColumns,
                [
                    [
                        StaticReportTestData.VersionId,
                        StaticReportTestData.StaticReportId,
                        "Help using D2R2 guidance",
                        (byte)1,
                        StaticReportTestData.EffectiveDateFrom,
                        null,
                        true,
                        false,
                        4096
                    ]
                ])
            ]
        });

        var versions = await CreateRepository().GetCurrentAsync(true, CancellationToken.None);

        versions.Should().ContainSingle();
        versions[0].Title.Should().Be("Help using D2R2 guidance");
        versions[0].IsCurrent.Should().BeTrue();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("IsUserManual").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetCurrentAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetCurrent, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCurrentAsync(true, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetHistoryAsync_MapsRowsAndPassesParameters()
    {
        connection.Script(StaticReportStoredProcedures.GetHistory, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(VersionColumns,
                [
                    [
                        StaticReportTestData.VersionId,
                        StaticReportTestData.StaticReportId,
                        "Help using D2R2 guidance",
                        (byte)0,
                        StaticReportTestData.EffectiveDateFrom.AddMonths(-1),
                        StaticReportTestData.EffectiveDateFrom,
                        true,
                        false,
                        2048
                    ]
                ])
            ]
        });

        var versions = await CreateRepository().GetHistoryAsync(StaticReportTestData.StaticReportId, CancellationToken.None);

        versions.Should().ContainSingle();
        versions[0].IsCurrent.Should().BeFalse();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("StaticReportId").WhoseValue.Should().Be(StaticReportTestData.StaticReportId);
    }

    [Fact]
    public async Task GetDataAsync_ReturnsNull_WhenNoVersionExists()
    {
        connection.Script(StaticReportStoredProcedures.GetData, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("PdfData", "IsPublic", "Title")]
        });

        var data = await CreateRepository().GetDataAsync(StaticReportTestData.VersionId, CancellationToken.None);

        data.Should().BeNull();
    }

    [Fact]
    public async Task GetDataAsync_ReturnsPersistedBytes()
    {
        connection.Script(StaticReportStoredProcedures.GetData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PdfData", "IsPublic", "Title"], [[StaticReportTestData.PdfBytes, false, "Help using D2R2 guidance"]])]
        });

        var data = await CreateRepository().GetDataAsync(StaticReportTestData.VersionId, CancellationToken.None);

        data.Should().NotBeNull();
        data!.PdfData.Should().Equal(StaticReportTestData.PdfBytes);
        data.Title.Should().Be("Help using D2R2 guidance");
    }

    [Fact]
    public async Task UploadAsync_PassesParameters()
    {
        connection.Script(StaticReportStoredProcedures.Upload, new FakeCommandScript());

        await CreateRepository().UploadAsync("Help using D2R2 guidance", StaticReportTestData.PdfBytes, true, false, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("Title").WhoseValue.Should().Be("Help using D2R2 guidance");
        executed.Parameters.Should().ContainKey("IsUserManual").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task UploadAsync_ThrowsConcurrencyException_OnUserRaisedError()
    {
        connection.Script(StaticReportStoredProcedures.Upload, new FakeCommandScript
        {
            Throws = new FakeDbException("You cannot upload a user manual with the same title as an existing static report")
        });

        var act = () => CreateRepository().UploadAsync("Help using D2R2 guidance", StaticReportTestData.PdfBytes, true, false, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task DeleteAsync_PassesParameters()
    {
        connection.Script(StaticReportStoredProcedures.Delete, new FakeCommandScript());

        await CreateRepository().DeleteAsync(StaticReportTestData.VersionId, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("StaticReportVersionId").WhoseValue.Should().Be(StaticReportTestData.VersionId);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsConcurrencyException_WhenVersionIsNotCurrent()
    {
        connection.Script(StaticReportStoredProcedures.Delete, new FakeCommandScript
        {
            Throws = new FakeDbException("You cannot delete this static report version because it is not current")
        });

        var act = () => CreateRepository().DeleteAsync(StaticReportTestData.VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => connection;
    }
}
