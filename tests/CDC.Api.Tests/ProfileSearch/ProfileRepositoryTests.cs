using System.Data;
using System.Linq;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

public sealed class ProfileRepositoryTests : IDisposable
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
        var scenario = profile.WhatIfScenarios.Should().ContainSingle().Subject;
        scenario.ScenarioId.Should().Be(ScenarioId);
        scenario.DraftVersions.Should().ContainSingle().Which.VersionId.Should().Be(scenarioVersionId);
        scenario.PublishedVersions.Should().BeEmpty();
        profile.AffectedSpecies.Should().BeEmpty();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(ProfileStoredProcedures.GetAllProfiles);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("UserId").WhoseValue.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task GetAllProfilesAsync_MapsEffectiveDateToOntoSupersededVersionsOnly()
    {
        var currentVersionId = Guid.NewGuid();
        var supersededVersionId = Guid.NewGuid();

        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "Bovine tuberculosis"]]),
                FakeResultSet.Empty(Rs2Columns),
                new FakeResultSet(Rs3Columns,
                [
                    [currentVersionId, ProfileAId, ProfileAId, 11, 0, "Published", Utc(2026, 3, 7), null, true, null],
                    [supersededVersionId, ProfileAId, ProfileAId, 10, 2, "Published", Utc(2025, 1, 16), Utc(2026, 3, 7), true, null]
                ])
            ]
        });

        var profiles = await CreateRepository().GetAllProfilesAsync(CancellationToken.None);

        var versions = profiles.Should().ContainSingle().Subject.PublishedVersions;
        versions.Single(version => version.VersionId == currentVersionId).EffectiveToUtc.Should().BeNull();

        var superseded = versions.Single(version => version.VersionId == supersededVersionId);
        superseded.EffectiveToUtc.Should().Be(Utc(2026, 3, 7));
        superseded.VersionMinor.Should().Be(2);
    }

    [Fact]
    public async Task GetAllProfilesAsync_KeepsEachScenarioLineagesVersionHistoryIndependent()
    {
        var scenarioBId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var currentSituationPublished = Guid.NewGuid();
        var scenarioAPublished = Guid.NewGuid();
        var scenarioADraft = Guid.NewGuid();
        var scenarioBDraft = Guid.NewGuid();

        connection.Script(ProfileStoredProcedures.GetAllProfiles, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Rs1Columns, [[ProfileAId, "African Horse Sickness"]]),
                FakeResultSet.Empty(Rs2Columns),
                new FakeResultSet(Rs3Columns,
                [
                    [currentSituationPublished, ProfileAId, ProfileAId, 1, 0, "Published", Utc(2026, 1, 1), null, true, Utc(2026, 1, 1)],
                    [scenarioAPublished, ScenarioId, ProfileAId, 1, 0, "Published", Utc(2026, 1, 2), null, false, Utc(2026, 1, 2)],
                    [scenarioADraft, ScenarioId, ProfileAId, 2, 0, "Draft", Utc(2026, 1, 3), null, false, Utc(2026, 1, 3)],
                    [scenarioBDraft, scenarioBId, ProfileAId, 1, 0, "Draft", Utc(2026, 1, 4), null, false, Utc(2026, 1, 4)]
                ])
            ]
        });

        var profile = (await CreateRepository().GetAllProfilesAsync(CancellationToken.None)).Should().ContainSingle().Subject;

        // The profile's own (current-situation) history must contain only its own version.
        profile.PublishedVersions.Should().ContainSingle().Which.VersionId.Should().Be(currentSituationPublished);
        profile.DraftVersions.Should().BeEmpty();

        profile.WhatIfScenarios.Should().HaveCount(2);

        var scenarioA = profile.WhatIfScenarios.Single(scenario => scenario.ScenarioId == ScenarioId);
        scenarioA.PublishedVersions.Should().ContainSingle().Which.VersionId.Should().Be(scenarioAPublished);
        scenarioA.DraftVersions.Should().ContainSingle().Which.VersionId.Should().Be(scenarioADraft);

        var scenarioB = profile.WhatIfScenarios.Single(scenario => scenario.ScenarioId == scenarioBId);
        scenarioB.DraftVersions.Should().ContainSingle().Which.VersionId.Should().Be(scenarioBDraft);
        scenarioB.PublishedVersions.Should().BeEmpty();
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
        profile.WhatIfScenarios.Should().BeEmpty();
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
