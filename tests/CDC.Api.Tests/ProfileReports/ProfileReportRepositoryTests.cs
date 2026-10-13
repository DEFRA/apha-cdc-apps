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

    [Fact]
    public async Task GetDraftProfileVersionsAsync_MapsTheFirstResultSet_AndAsksForDraftVersions()
    {
        var versionId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetProfileHistory, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    [
                        "Id", "ProfileId", "Title", "VersionMajor", "VersionMinor", "Name", "EffectiveDateFrom", "EffectiveDateTo",
                        "IsLatestVersion", "ScenarioTitle", "ParentId", "IsPublic", "ProfileStatusId", "ProfileStatusName"
                    ],
                    [
                        [
                            versionId, ProfileReportTestData.ProfileId, "Avian influenza", (byte)2, (byte)1, "Draft",
                            new DateTime(2026, 9, 11), null, true, "What if", Guid.Empty, false, Guid.NewGuid(), "Active"
                        ],
                        [
                            Guid.NewGuid(), ProfileReportTestData.ProfileId, "Avian influenza", (byte)2, (byte)0, "Draft",
                            new DateTime(2026, 1, 1), new DateTime(2026, 9, 11), false, null, Guid.Empty, false, Guid.NewGuid(), null
                        ]
                    ]),
                FakeResultSet.Empty("Id", "Name", "AffectedSpeciesType", "IsActive")
            ]
        });

        var versions = await CreateRepository().GetDraftProfileVersionsAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        versions.Should().HaveCount(2);
        versions[0].Id.Should().Be(versionId);
        versions[0].FullTitle.Should().Be("Avian influenza (What if)");
        versions[0].VersionMajor.Should().Be(2);
        versions[0].VersionMinor.Should().Be(1);
        versions[0].EffectiveDateTo.Should().BeNull();
        versions[0].ProfileStatusName.Should().Be("Active");
        versions[1].FullTitle.Should().Be("Avian influenza");
        versions[1].EffectiveDateTo.Should().Be(new DateTime(2026, 9, 11));
        versions[1].ProfileStatusName.Should().BeEmpty();

        var command = connection.Executed.Should().ContainSingle().Subject;
        command.Parameters["ProfileId"].Should().Be(ProfileReportTestData.ProfileId);
        command.Parameters["StateName"].Should().Be("Draft");
        command.Parameters["PublicOnly"].Should().Be(false);
    }

    [Fact]
    public async Task GetDraftProfileVersionsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileHistory, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetDraftProfileVersionsAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
