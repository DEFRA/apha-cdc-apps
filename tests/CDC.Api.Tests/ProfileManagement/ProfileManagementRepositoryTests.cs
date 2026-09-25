using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileManagementRepository>> logger = new();

    public ProfileManagementRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileManagementRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetProfileAttributesAsync_ReturnsNull_WhenProfileDoesNotExist()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfile, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id", "Title")]
        });

        var profile = await CreateRepository().GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        profile.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAttributesAsync_MapsProfileAndAffectedSpecies()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfile, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Title", "ScenarioTitle", "ParentId", "ParentTitle", "CurrentDraftProfileVersionId",
                        "CurrentPublishedProfileVersionId", "CurrentPublicVersionId", "HasPublicScenarios", "ProfileStatusId", "LastUpdated"],
                    [[
                        ProfileManagementTestData.ProfileId, "Bovine tuberculosis", string.Empty, Guid.Empty, string.Empty,
                        ProfileManagementTestData.ProfileVersionId, Guid.Empty, Guid.Empty, false,
                        ProfileManagementTestData.ProfileStatusId, ProfileManagementTestData.RowVersion
                    ]]),
                new FakeResultSet(
                    ["SpeciesId", "Name", "Type", "IsActive"],
                    [[ProfileManagementTestData.SpeciesId, "Cattle", "Profiled", true]])
            ]
        });

        var profile = await CreateRepository().GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.Title.Should().Be("Bovine tuberculosis");
        profile.LastUpdated.Should().Equal(ProfileManagementTestData.RowVersion);
        profile.AffectedSpecies.Should().ContainSingle();
        profile.AffectedSpecies[0].Name.Should().Be("Cattle");
    }

    [Fact]
    public async Task GetAffectedSpeciesAsync_ReturnsNull_WhenSpeciesDoesNotExist()
    {
        connection.Script(ProfileManagementStoredProcedures.GetSpeciesNameById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("SpeciesId", "Name", "IsActive")]
        });

        var species = await CreateRepository().GetAffectedSpeciesAsync(ProfileManagementTestData.SpeciesId, CancellationToken.None);

        species.Should().BeNull();
    }

    [Fact]
    public async Task GetAffectedSpeciesAsync_ReturnsNameAndActiveState()
    {
        connection.Script(ProfileManagementStoredProcedures.GetSpeciesNameById, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["SpeciesId", "Name", "IsActive"], [[ProfileManagementTestData.SpeciesId, "Cattle", true]])]
        });

        var species = await CreateRepository().GetAffectedSpeciesAsync(ProfileManagementTestData.SpeciesId, CancellationToken.None);

        species.Should().NotBeNull();
        species!.Name.Should().Be("Cattle");
        species.IsActive.Should().BeTrue();
        species.SpeciesId.Should().Be(ProfileManagementTestData.SpeciesId);
    }

    [Fact]
    public async Task GetProfileStatusTypesAsync_MapsRows()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileStatusTypes, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "IsValidationComplete"],
                    [[ProfileManagementTestData.ProfileStatusId, "Draft", false]])
            ]
        });

        var statusTypes = await CreateRepository().GetProfileStatusTypesAsync(CancellationToken.None);

        statusTypes.Should().ContainSingle();
        statusTypes[0].Name.Should().Be("Draft");
    }

    [Fact]
    public async Task SetProfileVersionPublicAccessAsync_ExecutesProcedure()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileVersionPublicFlag, new FakeCommandScript());

        await CreateRepository().SetProfileVersionPublicAccessAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        connection.Executed.Should().ContainSingle()
            .Which.CommandText.Should().Be(ProfileManagementStoredProcedures.UpdateProfileVersionPublicFlag);
    }

    [Fact]
    public async Task UpdateProfileStatusAsync_ExecutesProcedure()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileStatus, new FakeCommandScript());

        await CreateRepository().UpdateProfileStatusAsync(
            ProfileManagementTestData.ProfileId,
            ProfileManagementTestData.ProfileStatusId,
            CancellationToken.None);

        connection.Executed.Should().ContainSingle()
            .Which.CommandText.Should().Be(ProfileManagementStoredProcedures.UpdateProfileStatus);
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_ThrowsConcurrencyException_OnRowVersionMismatch()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfile, new FakeCommandScript
        {
            Throws = new FakeDbException("The profile has been edited by another user.")
        });

        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            LastUpdated = ProfileManagementTestData.RowVersion
        };

        var act = () => CreateRepository().UpdateProfileAttributesAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain(ProfileManagementTestData.ProfileId.ToString());
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_ReturnsNull_WhenVersionDoesNotExist()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("ProfileId")]
        });

        var result = await CreateRepository().DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_DeletesEachAffectedSpecies_ThenTheVersion()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["ProfileId"], [[ProfileManagementTestData.ProfileId]]),
                new FakeResultSet(["SpeciesId"], [[ProfileManagementTestData.SpeciesId]])
            ]
        });
        connection.Script(ProfileManagementStoredProcedures.DeleteProfileVersionSpecies, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.DeleteProfileVersion, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?>
            {
                ["NextLatestProfileVersionId"] = null,
                ["ProfileDeleted"] = true
            }
        });

        var result = await CreateRepository().DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsProfileDeleted.Should().BeTrue();
        result.NextLatestProfileVersionId.Should().BeNull();

        connection.Executed.Should().Contain(command => command.CommandText == ProfileManagementStoredProcedures.DeleteProfileVersionSpecies);
        connection.Executed.Should().Contain(command => command.CommandText == ProfileManagementStoredProcedures.DeleteProfileVersion);
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ThrowsConcurrencyException_WhenSourceIsNotLatestVersion()
    {
        ScriptCurrentVersion(isLatestVersion: false, isPublished: false, hasActiveProfiledSpecies: true);

        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        var act = () => CreateRepository().CreateNewProfileVersionAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain("not the latest version");
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ThrowsConcurrencyException_WhenSourceAlreadyPublishedAndPublishing()
    {
        ScriptCurrentVersion(isLatestVersion: true, isPublished: true, hasActiveProfiledSpecies: true);

        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        var act = () => CreateRepository().CreateNewProfileVersionAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain("already published");
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ThrowsConcurrencyException_WhenNoActiveProfiledSpecies()
    {
        ScriptCurrentVersion(isLatestVersion: true, isPublished: false, hasActiveProfiledSpecies: false);

        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        var act = () => CreateRepository().CreateNewProfileVersionAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain("no active profiled species");
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_CreatesVersionAndRecalculatesPrioritisation_WhenPublishing()
    {
        ScriptCurrentVersion(isLatestVersion: true, isPublished: false, hasActiveProfiledSpecies: true);
        connection.Script(ProfileManagementStoredProcedures.GetProfile, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Title", "ScenarioTitle", "ParentId", "ParentTitle"],
                    [[ProfileManagementTestData.ProfileId, "Bovine tuberculosis", string.Empty, Guid.Empty, string.Empty]])
            ]
        });
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileVersionCurrency, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.InsertProfileVersion, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.InsertProfileVersionSpecies, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.CalculatePrioritisation, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.CalculatePrioritisationScore, new FakeCommandScript());

        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: true);

        var newProfileVersionId = await CreateRepository().CreateNewProfileVersionAsync(command, CancellationToken.None);

        newProfileVersionId.Should().NotBe(Guid.Empty);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.InsertProfileVersion);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.InsertProfileVersionSpecies);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.CalculatePrioritisation);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.CalculatePrioritisationScore);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    /// <summary>Scripts <c>spgProfileVersionInfoById</c> with one active "Profiled" affected species.</summary>
    private void ScriptCurrentVersion(bool isLatestVersion, bool isPublished, bool hasActiveProfiledSpecies)
    {
        var columns = new[]
        {
            "col0", "ProfileId", "Title", "VersionMajor", "VersionMinor", "State",
            "col6", "col7", "IsLatestVersion", "ScenarioTitle", "ParentProfileId", "col11", "ProfileStatusId"
        };

        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    columns,
                    [[
                        Guid.Empty, ProfileManagementTestData.ProfileId, "Bovine tuberculosis", (byte)1, (byte)2,
                        isPublished ? "Published" : "Draft", null, null, isLatestVersion, string.Empty, Guid.Empty, null,
                        ProfileManagementTestData.ProfileStatusId
                    ]]),
                new FakeResultSet(
                    ["SpeciesId", "Name", "Type", "IsActive"],
                    [[ProfileManagementTestData.SpeciesId, "Cattle", "Profiled", hasActiveProfiledSpecies]])
            ]
        });
    }

    [Fact]
    public async Task GetProfileAttributesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfile, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetNewProfileDefaultsAsync_ReturnsNull_WhenSourceVersionDoesNotExist()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("ProfileId")]
        });

        var defaults = await CreateRepository().GetNewProfileDefaultsAsync(
            ProfileManagementTestData.ProfileVersionId,
            isWhatIfScenario: false,
            CancellationToken.None);

        defaults.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetNewProfileDefaultsAsync_MapsTitleOrParent_DependingOnScenarioKind(bool isWhatIfScenario)
    {
        var columns = new[]
        {
            "col0", "ProfileId", "Title", "col3", "col4", "col5",
            "col6", "col7", "col8", "ScenarioTitle", "ParentProfileId", "col11", "ProfileStatusId"
        };

        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    columns,
                    [[
                        Guid.Empty, ProfileManagementTestData.ProfileId, "Bovine tuberculosis", null, null, null,
                        null, null, null, "Scenario A", ProfileManagementTestData.ProfileId, null,
                        ProfileManagementTestData.ProfileStatusId
                    ]]),
                new FakeResultSet(
                    ["SpeciesId", "Name", "Type", "IsActive"],
                    [[ProfileManagementTestData.SpeciesId, "Cattle", "Profiled", true]])
            ]
        });

        var defaults = await CreateRepository().GetNewProfileDefaultsAsync(
            ProfileManagementTestData.ProfileVersionId,
            isWhatIfScenario,
            CancellationToken.None);

        defaults.Should().NotBeNull();
        defaults!.ScenarioTitle.Should().Be("Scenario A");
        defaults.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
        defaults.AffectedSpecies.Should().ContainSingle();

        if (isWhatIfScenario)
        {
            defaults.ParentId.Should().Be(ProfileManagementTestData.ProfileId);
            defaults.Title.Should().BeEmpty();
        }
        else
        {
            defaults.Title.Should().Be("Bovine tuberculosis");
            defaults.ParentId.Should().Be(Guid.Empty);
        }
    }

    [Fact]
    public async Task CreateProfileAsync_InsertsProfileVersionAndAffectedSpecies()
    {
        connection.Script(ProfileManagementStoredProcedures.InsertProfile, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = ProfileManagementTestData.NewRowVersion }
        });
        connection.Script(ProfileManagementStoredProcedures.InsertProfileVersion, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.InsertProfileVersionSpecies, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData, new FakeCommandScript());

        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            Title = "Anthrax",
            CloneProfileVersionId = Guid.Empty,
            ParentId = Guid.Empty,
            ProfileStatusId = ProfileManagementTestData.ProfileStatusId,
            AffectedSpeciesInsertList =
            [
                new AffectedSpeciesInsertDto
                {
                    ProfileVersionId = ProfileManagementTestData.ProfileVersionId,
                    SpeciesId = ProfileManagementTestData.SpeciesId,
                    Type = "Profiled"
                }
            ]
        };

        var result = await CreateRepository().CreateProfileAsync(command, CancellationToken.None);

        result.NewProfileId.Should().Be(ProfileManagementTestData.ProfileId);
        result.NewLastUpdated.Should().Equal(ProfileManagementTestData.NewRowVersion);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.InsertProfileVersionSpecies);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.UpdateProfileVersionSpeciesTradeData);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileAttributesAsync_UpdatesAttributesAndAffectedSpecies()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfile, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = ProfileManagementTestData.NewRowVersion }
        });
        connection.Script(ProfileManagementStoredProcedures.DeleteProfileVersionSpecies, new FakeCommandScript());
        connection.Script(ProfileManagementStoredProcedures.InsertProfileVersionSpecies, new FakeCommandScript());

        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            Title = "Anthrax",
            LastUpdated = ProfileManagementTestData.RowVersion,
            AffectedSpeciesDeleteList = [new AffectedSpeciesDeleteDto { ProfileVersionId = ProfileManagementTestData.ProfileVersionId, SpeciesId = ProfileManagementTestData.SpeciesId }],
            AffectedSpeciesInsertList =
            [
                new AffectedSpeciesInsertDto
                {
                    ProfileVersionId = ProfileManagementTestData.ProfileVersionId,
                    SpeciesId = ProfileManagementTestData.SpeciesId,
                    Type = "Other"
                }
            ]
        };

        var newLastUpdated = await CreateRepository().UpdateProfileAttributesAsync(command, CancellationToken.None);

        newLastUpdated.Should().Equal(ProfileManagementTestData.NewRowVersion);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.DeleteProfileVersionSpecies);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileManagementStoredProcedures.InsertProfileVersionSpecies);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task SetProfileVersionPublicAccessAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileVersionPublicFlag, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().SetProfileVersionPublicAccessAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateProfileStatusAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.UpdateProfileStatus, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().UpdateProfileStatusAsync(
            ProfileManagementTestData.ProfileId,
            ProfileManagementTestData.ProfileStatusId,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileStatusTypesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileStatusTypes, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileStatusTypesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetAffectedSpeciesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetSpeciesNameById, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetAffectedSpeciesAsync(ProfileManagementTestData.SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetNewProfileDefaultsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetNewProfileDefaultsAsync(ProfileManagementTestData.ProfileVersionId, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task CreateProfileAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.InsertProfile, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            Title = "Anthrax",
            CloneProfileVersionId = Guid.Empty,
            ParentId = Guid.Empty,
            ProfileStatusId = ProfileManagementTestData.ProfileStatusId
        };

        var act = () => CreateRepository().CreateProfileAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileManagementStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        var act = () => CreateRepository().CreateNewProfileVersionAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a concurrency failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
