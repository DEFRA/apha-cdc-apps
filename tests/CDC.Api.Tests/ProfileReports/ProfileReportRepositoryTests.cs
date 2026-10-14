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
                    ["Id", "ReportName", "DisplayName", "HasPdfData", "FileSize", "DisplayPublic"],
                    [[ProfileReportTestData.ProfileReportId, "FullProfileGUID", "Full Profile Report", true, 4096, true]])
            ]
        });

        var reports = await CreateRepository().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, true, CancellationToken.None);

        var report = reports.Should().ContainSingle().Subject;
        report.Id.Should().Be(ProfileReportTestData.ProfileReportId);
        report.ReportName.Should().Be("FullProfileGUID");
        report.DisplayName.Should().Be("Full Profile Report");
        report.HasPdfData.Should().BeTrue();
        report.FileSize.Should().Be(4096);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("IsAuthenticated").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task GetProfileVersionReportsAsync_ReadsAReportWithNoPdfYet_WhoseFileSizeIsNull()
    {
        // The procedure LEFT JOINs the stored PDFs, so a report that has not been generated has a NULL size.
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReports, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "ReportName", "DisplayName", "HasPdfData", "FileSize", "DisplayPublic"],
                    [[ProfileReportTestData.ProfileReportId, "QABriefGUID", "QA brief", false, null, false]])
            ]
        });

        var reports = await CreateRepository().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, false, CancellationToken.None);

        var report = reports.Should().ContainSingle().Subject;
        report.HasPdfData.Should().BeFalse();
        report.FileSize.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileVersionReportsAsync_ReadsAFileSizeReturnedAsABigInt()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionReports, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "ReportName", "DisplayName", "HasPdfData", "FileSize", "DisplayPublic"],
                    [[ProfileReportTestData.ProfileReportId, "SummaryProfileGUID", "Summary profile", true, 123456L, true]])
            ]
        });

        var reports = await CreateRepository().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, true, CancellationToken.None);

        reports.Should().ContainSingle().Which.FileSize.Should().Be(123456);
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

    [Fact]
    public async Task GetProfileVersionHeaderAsync_MapsTheFirstResultSet()
    {
        var versionId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
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
                            versionId, ProfileReportTestData.ProfileId, "Avian influenza", (byte)2, (byte)1, "Published",
                            new DateTime(2026, 9, 11), null, true, "What if", Guid.Empty, true, Guid.NewGuid(), "Active"
                        ]
                    ]),
                FakeResultSet.Empty("Id", "Name", "AffectedSpeciesType", "IsActive")
            ]
        });

        var header = await CreateRepository().GetProfileVersionHeaderAsync(versionId, CancellationToken.None);

        header.Should().NotBeNull();
        header!.Id.Should().Be(versionId);
        header.ProfileId.Should().Be(ProfileReportTestData.ProfileId);
        header.FullTitle.Should().Be("Avian influenza (What if)");
        header.VersionLabel.Should().Be("2.1");
        header.IsPublished.Should().BeTrue();
        header.IsCurrent.Should().BeTrue();
        header.IsPublic.Should().BeTrue();
        header.ProfileStatusName.Should().Be("Active");
        connection.Executed.Should().ContainSingle().Subject.Parameters["Id"].Should().Be(versionId);
    }

    [Fact]
    public async Task GetProfileVersionHeaderAsync_TreatsADraftStateAsNotPublished_AndNullsAsEmpty()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
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
                            Guid.NewGuid(), Guid.NewGuid(), "Anthrax", (byte)1, (byte)0, "Draft",
                            new DateTime(2025, 1, 1), new DateTime(2026, 1, 1), false, null, null, null, null, null
                        ]
                    ])
            ]
        });

        var header = await CreateRepository().GetProfileVersionHeaderAsync(Guid.NewGuid(), CancellationToken.None);

        header!.IsPublished.Should().BeFalse();
        header.IsCurrent.Should().BeFalse();
        header.FullTitle.Should().Be("Anthrax");
        header.ProfileStatusName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileVersionHeaderAsync_ReturnsNull_WhenTheVersionDoesNotExist()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id")]
        });

        var header = await CreateRepository().GetProfileVersionHeaderAsync(Guid.NewGuid(), CancellationToken.None);

        header.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileVersionHeaderAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileVersionHeaderAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
