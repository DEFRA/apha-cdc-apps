using System.Data;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileReportRepository>> logger = new();

    public ProfileReportRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileReportRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetProfileVersionReportsAsync_MapsRowsAndPassesParameters()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReports, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "ReportName", "DisplayName", "HasPdfData", "FileSize"],
                    [[ProfileReportTestData.ProfileReportId, "FullProfileGUID", "Full Profile Report", true, 4096]])
            ]
        });

        var reports = await CreateRepository().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, true, CancellationToken.None);

        reports.Should().ContainSingle();
        reports[0].DisplayName.Should().Be("Full Profile Report");

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("IsAuthenticated").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetProfileVersionReportsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReports, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileReportDataAsync_ReturnsNull_WhenNoDocumentGenerated()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReportData, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("PdfData")]
        });

        var reportData = await CreateRepository().GetProfileReportDataAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        reportData.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileReportDataAsync_ReturnsPersistedBytes()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReportData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PdfData"], [[ProfileReportTestData.ReportBytes]])]
        });

        var reportData = await CreateRepository().GetProfileReportDataAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        reportData.Should().NotBeNull();
        reportData!.ReportData.Should().Equal(ProfileReportTestData.ReportBytes);
    }

    [Fact]
    public async Task GetProfileReportDataAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReportData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileReportDataAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task CreateProfileReportAsync_PersistsDataAndReturnsId()
    {
        connection.Script(ProfileReportStoredProcedures.InsertProfileVersionReportData, new FakeCommandScript());

        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };

        var reportId = await CreateRepository().CreateProfileReportAsync(command, CancellationToken.None);

        reportId.Should().Be(ProfileReportTestData.ProfileReportId);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("PdfData").WhoseValue.Should().BeEquivalentTo(ProfileReportTestData.ReportBytes);
    }

    [Fact]
    public async Task CreateProfileReportAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.InsertProfileVersionReportData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };

        var act = () => CreateRepository().CreateProfileReportAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
