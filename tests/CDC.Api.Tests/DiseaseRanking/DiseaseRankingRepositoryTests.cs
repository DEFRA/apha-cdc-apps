using System.Data;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.DiseaseRanking;

public sealed class DiseaseRankingRepositoryTests : IDisposable
{
    private static readonly Guid FilterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SpeciesId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid VersionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly string[] RankColumns =
    [
        "ProfileId", "SpeciesId", "Name", "Rank", "IsEndemic", "Title", "SpeciesName", "ProfileVersionState", "IsValidationComplete", "Id"
    ];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<DiseaseRankingRepository>> logger = new();

    public DiseaseRankingRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private DiseaseRankingRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    private static object?[] RankRow(string category, int rank, bool isEndemic = true, string state = "Published", bool? validationComplete = true) =>
        [ProfileId, SpeciesId, category, rank, isEndemic, "<em>Anthrax</em>", "Cattle", state, validationComplete, VersionId];

    [Fact]
    public async Task GetFiltersAsync_MapsIdAndName()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetFilterNames, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["FilterId", "DiseaseName", "DateInserted"], [[FilterId, "All viral diseases", DateTime.UtcNow]])]
        });

        var filters = await CreateRepository().GetFiltersAsync(CancellationToken.None);

        filters.Should().ContainSingle().Which.Should().Be(new DiseaseRankingFilter(FilterId, "All viral diseases"));
    }

    [Fact]
    public async Task GetFiltersAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetFilterNames, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetFiltersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRankedProfilesAsync_WithoutFilter_FoldsCategoryRowsIntoOneProfile()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetRankProfileData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    RankColumns,
                    [
                        RankRow("Public Health", 5),
                        RankRow("Welfare", 2),
                        RankRow("Wider Society", 1),
                        RankRow("International Trade", 4),
                        RankRow("Post Mitigation Risk", 3),
                        RankRow("Potential to add value", 9),
                        RankRow("Something else", 7)
                    ])
            ]
        });

        var profiles = await CreateRepository().GetRankedProfilesAsync(null, CancellationToken.None);

        var profile = profiles.Should().ContainSingle().Subject;
        profile.ProfileId.Should().Be(ProfileId);
        profile.SpeciesId.Should().Be(SpeciesId);
        profile.ProfileTitle.Should().Be("<em>Anthrax</em>");
        profile.SpeciesName.Should().Be("Cattle");
        profile.IsExotic.Should().BeFalse();
        profile.IsAwaitingValidation.Should().BeFalse();
        profile.PublicHealthRank.Should().Be(5);
        profile.AnimalWelfareRank.Should().Be(2);
        profile.WiderSocietyRank.Should().Be(1);
        profile.InternationalTradeRank.Should().Be(4);
        profile.RiskRank.Should().Be(3);
        connection.Executed.Should().ContainSingle().Which.Parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Published", false, false)]
    [InlineData("Draft", false, true)]
    [InlineData("Draft", true, false)]
    public async Task GetRankedProfilesAsync_WorksOutWhetherTheProfileIsAwaitingValidation(string state, bool validationComplete, bool expected)
    {
        connection.Script(DiseaseRankingStoredProcedures.GetRankProfileData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(RankColumns, [RankRow("Public Health", 1, isEndemic: false, state: state, validationComplete: validationComplete)])]
        });

        var profile = (await CreateRepository().GetRankedProfilesAsync(null, CancellationToken.None)).Single();

        profile.IsAwaitingValidation.Should().Be(expected);
        profile.IsExotic.Should().BeTrue();
    }

    [Fact]
    public async Task GetRankedProfilesAsync_TreatsNullValidationFlagAsIncomplete()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetRankProfileData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(RankColumns, [RankRow("Public Health", 1, state: "Draft", validationComplete: null)])]
        });

        var profile = (await CreateRepository().GetRankedProfilesAsync(null, CancellationToken.None)).Single();

        profile.IsAwaitingValidation.Should().BeTrue();
    }

    [Fact]
    public async Task GetRankedProfilesAsync_WithFilter_UsesFilterProcedureAndPassesName()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetFilterRankProfileData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(RankColumns, [RankRow("Public Health", 1)])]
        });

        await CreateRepository().GetRankedProfilesAsync("All viral diseases", CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(DiseaseRankingStoredProcedures.GetFilterRankProfileData);
        executed.Parameters.Should().ContainKey("DiseaseName").WhoseValue.Should().Be("All viral diseases");
    }

    [Fact]
    public async Task GetRankedProfilesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(DiseaseRankingStoredProcedures.GetRankProfileData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetRankedProfilesAsync(null, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetFiltersAsync_Throws_WhenConnectionIsNotADbConnection()
    {
        var repository = new DiseaseRankingRepository(new NonDbConnectionFactory(), logger.Object);

        var act = () => repository.GetFiltersAsync(CancellationToken.None);

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

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
