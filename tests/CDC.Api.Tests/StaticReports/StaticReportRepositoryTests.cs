using System.Data;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public sealed class StaticReportRepositoryTests : IDisposable
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

    private static readonly string[] ReportColumns =
    [
        "Id", "StaticReportId", "Title", "VersionMajor", "EffectiveDateFrom", "EffectiveDateTo", "IsUserManual", "IsPublic", "FileSize"
    ];

    [Fact]
    public async Task GetCurrentStaticReportsAsync_MapsEveryColumn()
    {
        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        connection.Script(StaticReportStoredProcedures.GetCurrentStaticReports, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ReportColumns,
                    [[VersionId, StaticReportId, "Help using D2R2", 2, effectiveFrom, null, true, true, 4096]])
            ]
        });

        var reports = await CreateRepository().GetCurrentStaticReportsAsync(true, false, CancellationToken.None);

        var report = reports.Should().ContainSingle().Subject;
        report.Id.Should().Be(VersionId);
        report.StaticReportId.Should().Be(StaticReportId);
        report.Title.Should().Be("Help using D2R2");
        report.VersionMajor.Should().Be(2);
        report.EffectiveDateFrom.Should().Be(effectiveFrom);
        report.EffectiveDateTo.Should().BeNull();
        report.IsUserManual.Should().BeTrue();
        report.IsPublic.Should().BeTrue();
        report.FileSize.Should().Be(4096);
        report.IsCurrent.Should().BeTrue();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(StaticReportStoredProcedures.GetCurrentStaticReports);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("IsUserManual").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_DefaultsNullableColumns()
    {
        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        connection.Script(StaticReportStoredProcedures.GetCurrentStaticReports, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ReportColumns,
                    [[VersionId, StaticReportId, null, 1, effectiveFrom, effectiveTo, null, null, null]])
            ]
        });

        var reports = await CreateRepository().GetCurrentStaticReportsAsync(false, true, CancellationToken.None);

        var report = reports.Should().ContainSingle().Subject;
        report.Title.Should().Be(string.Empty);
        report.EffectiveDateTo.Should().Be(effectiveTo);
        report.IsUserManual.Should().BeFalse();
        report.IsPublic.Should().BeFalse();
        report.FileSize.Should().Be(0);
        report.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_ReturnsEmpty_WhenThereAreNoReports()
    {
        connection.Script(StaticReportStoredProcedures.GetCurrentStaticReports, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(ReportColumns, [])]
        });

        var reports = await CreateRepository().GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        reports.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetCurrentStaticReports, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetStaticReportHistoryAsync_MapsRowsAndPassesParameters()
    {
        var effectiveFrom = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        connection.Script(StaticReportStoredProcedures.GetStaticReportHistory, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ReportColumns,
                    [[VersionId, StaticReportId, "Help using D2R2 v1", 1, effectiveFrom, null, false, false, 2048]])
            ]
        });

        var versions = await CreateRepository().GetStaticReportHistoryAsync(StaticReportId, true, CancellationToken.None);

        versions.Should().ContainSingle();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(StaticReportStoredProcedures.GetStaticReportHistory);
        executed.Parameters.Should().ContainKey("StaticReportId").WhoseValue.Should().Be(StaticReportId);
        executed.Parameters.Should().ContainKey("PublicOnly").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetStaticReportHistoryAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetStaticReportHistory, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetStaticReportHistoryAsync(StaticReportId, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetStaticReportDataAsync_ReturnsDocument_WhenFound()
    {
        var pdfBytes = new byte[] { 1, 2, 3 };

        connection.Script(StaticReportStoredProcedures.GetStaticReportVersionData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["PdfData", "IsPublic", "Title"], [[pdfBytes, true, "Help using D2R2"]])
            ]
        });

        var document = await CreateRepository().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        document.Should().NotBeNull();
        document!.PdfData.Should().BeEquivalentTo(pdfBytes);
        document.Title.Should().Be("Help using D2R2");
    }

    [Fact]
    public async Task GetStaticReportDataAsync_DefaultsTitle_WhenTitleIsNull()
    {
        var pdfBytes = new byte[] { 1, 2, 3 };

        connection.Script(StaticReportStoredProcedures.GetStaticReportVersionData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["PdfData", "IsPublic", "Title"], [[pdfBytes, true, null]])
            ]
        });

        var document = await CreateRepository().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        document.Should().NotBeNull();
        document!.Title.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetStaticReportDataAsync_ReturnsNull_WhenNoRowExists()
    {
        connection.Script(StaticReportStoredProcedures.GetStaticReportVersionData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PdfData", "IsPublic", "Title"], [])]
        });

        var document = await CreateRepository().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        document.Should().BeNull();
    }

    [Fact]
    public async Task GetStaticReportDataAsync_ReturnsNull_WhenPdfDataColumnIsNull()
    {
        connection.Script(StaticReportStoredProcedures.GetStaticReportVersionData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PdfData", "IsPublic", "Title"], [[null, true, "Help using D2R2"]])]
        });

        var document = await CreateRepository().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        document.Should().BeNull();
    }

    [Fact]
    public async Task GetStaticReportDataAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.GetStaticReportVersionData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ExecutesStoredProcedure()
    {
        connection.Script(StaticReportStoredProcedures.DeleteStaticReportVersion, new FakeCommandScript());

        await CreateRepository().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(StaticReportStoredProcedures.DeleteStaticReportVersion);
        executed.Parameters.Should().ContainKey("StaticReportVersionId").WhoseValue.Should().Be(VersionId);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(StaticReportStoredProcedures.DeleteStaticReportVersion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task OpenConnectionAsync_Throws_WhenFactoryDoesNotReturnADbConnection()
    {
        var repository = new StaticReportRepository(new NonDbConnectionFactory(), logger.Object);

        var act = () => repository.GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class NonDbConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new Mock<IDbConnection>().Object;
    }
}
