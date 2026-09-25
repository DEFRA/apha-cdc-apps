using System.Data;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

public class ProfileRepositoryTests : IDisposable
{
    private static readonly Guid ProfileAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ScenarioId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrphanProfileId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileRepository>> logger = new();

    public ProfileRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Rs1Columns = ["Id", "Title"];
    private static readonly string[] Rs2Columns = ["ScenarioId", "ProfileId", "ScenarioTitle", "UserRole", "ProfileStatus"];
    private static readonly string[] Rs3Columns =
    [
        "Id", "ScenarioId", "ProfileId", "VersionMajor", "VersionMinor",
        "StateName", "EffectiveDateFrom", "EffectiveDateTo", "IsPublic", "LastContributionDate"
    ];

    [Fact]
    public async Task GetAllProfilesAsync_MapsPublishedDraftAndScenarioBuckets()
    {
        var publishedVersionId = Guid.NewGuid();
        var draftVersionId = Guid.NewGuid();
        var scenarioVersionId = Guid.NewGuid();

        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "Bovine tuberculosis"]]),
                FakeResultSet.Empty(Rs2Columns),
                new FakeResultSet(Rs3Columns,
                [
                    [publishedVersionId, ProfileAId, ProfileAId, 2, 0, "Published", Utc(2026, 1, 10), null, true, Utc(2026, 1, 12)],
                    [draftVersionId, ProfileAId, ProfileAId, 3, 0, "Draft", Utc(2026, 2, 1), null, false, null],
                    [scenarioVersionId, ScenarioId, ProfileAId, 1, 0, "Draft", Utc(2026, 1, 5), null, false, null]
                ])
            ]
        });

        var profiles = await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        var profile = profiles.Should().ContainSingle().Subject;
        profile.Id.Should().Be(ProfileAId);
        profile.Title.Should().Be("Bovine tuberculosis");
        profile.Status.Should().Be("Published");
        profile.IsPublic.Should().BeTrue();
        profile.CreatedAtUtc.Should().Be(Utc(2026, 1, 5));
        profile.ModifiedAtUtc.Should().Be(Utc(2026, 2, 1));

        profile.PublishedVersions.Should().ContainSingle().Which.VersionId.Should().Be(publishedVersionId);
        profile.DraftVersions.Should().ContainSingle().Which.VersionId.Should().Be(draftVersionId);
        profile.Scenarios.Should().ContainSingle().Which.VersionId.Should().Be(scenarioVersionId);
        profile.Scenarios[0].IsScenario.Should().BeTrue();
        profile.AffectedSpecies.Should().BeEmpty();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(ProfileStoredProcedures.GetAllProfiles);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("UserId").WhoseValue.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task GetAllProfilesAsync_DerivesDraftStatus_WhenOnlyDraftVersionsExist()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "Avian influenza"]]),
                FakeResultSet.Empty(Rs2Columns),
                new FakeResultSet(Rs3Columns,
                [
                    [Guid.NewGuid(), ProfileAId, ProfileAId, 1, 0, "Draft", Utc(2026, 3, 1), null, false, null]
                ])
            ]
        });

        var profile = (await CreateRepository().GetAllProfilesAsync(CancellationToken.None)).Should().ContainSingle().Subject;

        profile.Status.Should().Be("Draft");
        profile.PublishedVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProfilesAsync_DerivesDraftStatus_WhenProfileHasNoVersions()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "No versions yet"]]),
                FakeResultSet.Empty(Rs2Columns),
                FakeResultSet.Empty(Rs3Columns)
            ]
        });

        var profile = (await CreateRepository().GetAllProfilesAsync(CancellationToken.None)).Should().ContainSingle().Subject;

        profile.Status.Should().Be("Draft");
        profile.PublishedVersions.Should().BeEmpty();
        profile.DraftVersions.Should().BeEmpty();
        profile.Scenarios.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProfilesAsync_SkipsVersions_WhoseRootProfileIsNotInResultSet1()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "Bovine tuberculosis"]]),
                FakeResultSet.Empty(Rs2Columns),
                new FakeResultSet(Rs3Columns,
                [
                    [Guid.NewGuid(), OrphanProfileId, OrphanProfileId, 1, 0, "Published", Utc(2026, 1, 1), null, true, null]
                ])
            ]
        });

        var profiles = await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        profiles.Should().ContainSingle().Which.PublishedVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProfilesAsync_PreservesResultSet1Order_AcrossMultipleProfiles()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns,
                [
                    [ProfileBId, "Zebra disease"],
                    [ProfileAId, "Avian disease"]
                ]),
                FakeResultSet.Empty(Rs2Columns),
                FakeResultSet.Empty(Rs3Columns)
            ]
        });

        var profiles = await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        profiles.Select(profile => profile.Id).Should().ContainInOrder(ProfileBId, ProfileAId);
    }

    [Fact]
    public async Task GetAllProfilesAsync_ReturnsEmpty_WhenNoProfilesExist()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty(Rs1Columns),
                FakeResultSet.Empty(Rs2Columns),
                FakeResultSet.Empty(Rs3Columns)
            ]
        });

        var profiles = await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProfilesAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            Throws = new FakeDbException("Invalid object name 'Profile'")
        });

        var act = async () => await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();

        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetAllProfilesAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new ProfileRepository(new NonDbConnectionFactory(), logger.Object);

        var act = async () => await repository.GetAllProfilesAsync(CancellationToken.None);

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
