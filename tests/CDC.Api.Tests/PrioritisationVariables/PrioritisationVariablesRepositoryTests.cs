using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesRepositoryTests : IDisposable
{
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RankingRangeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 7, 209];
    private static readonly byte[] NewRowVersion = [0, 0, 0, 0, 0, 0, 7, 210];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<PrioritisationVariablesRepository>> logger = new();

    public PrioritisationVariablesRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private PrioritisationVariablesRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetCategoriesWithCriteriaAsync_MapsAllFourResultSets()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "LowerBound", "UpperBound", "LastUpdated"),
                new FakeResultSet(["Id", "Name"], [[CategoryId, "Animal welfare"]]),
                new FakeResultSet(
                    ["Id", "PrioritisationCategoryId", "Code", "Name", "Weighting"],
                    [[CriterionId, CategoryId, "C1", "Impact", 10]]),
                new FakeResultSet(
                    ["Id", "PrioritisationCriterionId", "CriterionValue", "Score"],
                    [[ValueId, CriterionId, "N/A", 5]])
            ]
        });

        var categories = await CreateRepository().GetCategoriesWithCriteriaAsync(CancellationToken.None);

        var category = categories.Should().ContainSingle().Subject;
        category.Id.Should().Be(CategoryId);
        category.Name.Should().Be("Animal welfare");
        var criterion = category.Criteria.Should().ContainSingle().Subject;
        criterion.Id.Should().Be(CriterionId);
        criterion.Weight.Should().Be(10);
        var value = criterion.Values.Should().ContainSingle().Subject;
        value.Id.Should().Be(ValueId);
        value.Score.Should().Be(5);
    }

    [Fact]
    public async Task GetCategoriesWithCriteriaAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCategoriesWithCriteriaAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateCriterionAsync_ExecutesTheStoredProcedureWithTheGivenValues()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterion, new FakeCommandScript());

        await CreateRepository().UpdateCriterionAsync(CriterionId, "Impact", 42, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters["CriterionId"].Should().Be(CriterionId);
        executed.Parameters["Weighting"].Should().Be(42);
        executed.Parameters["Name"].Should().Be("Impact");
    }

    [Fact]
    public async Task UpdateCriterionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateCriterionAsync(CriterionId, "Impact", 42, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateCriterionValueScoreAsync_ExecutesTheStoredProcedureWithTheGivenValues()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterionValue, new FakeCommandScript());

        await CreateRepository().UpdateCriterionValueScoreAsync(ValueId, 77, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters["CriterionValueId"].Should().Be(ValueId);
        executed.Parameters["Score"].Should().Be(77);
    }

    [Fact]
    public async Task UpdateCriterionValueScoreAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterionValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateCriterionValueScoreAsync(ValueId, 77, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRankingRangeAsync_ReadsOnlyTheFirstResultSet()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "LowerBound", "UpperBound", "LastUpdated"], [[RankingRangeId, 15, 35, RowVersion]])]
        });

        var rankingRange = await CreateRepository().GetRankingRangeAsync(CancellationToken.None);

        rankingRange.Id.Should().Be(RankingRangeId);
        rankingRange.LowerBound.Should().Be(15);
        rankingRange.UpperBound.Should().Be(35);
        rankingRange.RowVersion.Should().Equal(RowVersion);
    }

    [Fact]
    public async Task GetRankingRangeAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetRankingRangeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_ReturnsTheNewRowVersion()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateRankingRange, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = NewRowVersion }
        });

        var newRowVersion = await CreateRepository().UpdateRankingRangeAsync(15, 35, RowVersion, CancellationToken.None);

        newRowVersion.Should().Equal(NewRowVersion);
        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters["LowerBound"].Should().Be(15);
        executed.Parameters["UpperBound"].Should().Be(35);
        executed.Parameters["PrioritisationType"].Should().Be("Profile");
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_ThrowsConcurrencyException_OnRowVersionMismatch()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateRankingRange, new FakeCommandScript
        {
            Throws = new FakeDbException("The prioritisation variables have been edited by another user.")
        });

        var act = () => CreateRepository().UpdateRankingRangeAsync(15, 35, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_RethrowsAndLogs_OnUnrelatedDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateRankingRange, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateRankingRangeAsync(15, 35, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task PrioritiseDraftProfilesAsync_CalculatesEachDraftThenScoresAndCommits()
    {
        var firstVersion = Guid.NewGuid();
        var secondVersion = Guid.NewGuid();
        connection.Script(PrioritisationVariablesStoredProcedures.GetCurrentProfiles, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["ProfileVersionId"], [[firstVersion], [secondVersion]])]
        });

        var count = await CreateRepository().PrioritiseDraftProfilesAsync(CancellationToken.None);

        count.Should().Be(2);
        connection.Executed.Select(command => command.CommandText).Should().Equal(
            PrioritisationVariablesStoredProcedures.GetCurrentProfiles,
            PrioritisationVariablesStoredProcedures.CalculatePrioritisation,
            PrioritisationVariablesStoredProcedures.CalculatePrioritisation,
            PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore);
        connection.Executed[1].Parameters["ProfileVersionId"].Should().Be(firstVersion);
        connection.Executed[2].Parameters["ProfileVersionId"].Should().Be(secondVersion);
        connection.Executed.Should().OnlyContain(command => command.HadTransaction);
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.Committed.Should().BeTrue();
        transaction.RolledBack.Should().BeFalse();
    }

    [Theory]
    [InlineData(PrioritisationVariablesStoredProcedures.GetCurrentProfiles)]
    [InlineData(PrioritisationVariablesStoredProcedures.CalculatePrioritisation)]
    [InlineData(PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore)]
    public async Task PrioritiseDraftProfilesAsync_RollsBackAndRethrows_WhenAStepFails(string failingProcedure)
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetCurrentProfiles, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["ProfileVersionId"], [[Guid.NewGuid()]])]
        });
        connection.Script(failingProcedure, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().PrioritiseDraftProfilesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.RolledBack.Should().BeTrue();
        transaction.Committed.Should().BeFalse();
    }

    [Fact]
    public async Task RecalculatePrioritisationScoresAsync_ExecutesTheScoreProcedure()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore, new FakeCommandScript());

        await CreateRepository().RecalculatePrioritisationScoresAsync(CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task RecalculatePrioritisationScoresAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(
            PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore,
            new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().RecalculatePrioritisationScoresAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetScoreDataAsync_MapsAllNineResultSets()
    {
        var questionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var prioritisationQuestionId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var answerId = Guid.NewGuid();

        connection.Script(PrioritisationVariablesStoredProcedures.GetScoreData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["Id", "Section", "Question", "Text"], [[questionId, 1, 2, "Common question"]]),
                new FakeResultSet(["Id", "Seq", "Name"], [[categoryId, 1, "Welfare"]]),
                new FakeResultSet(
                    ["Id", "CategoryId", "Section", "Question", "Text", "Flag"],
                    [[prioritisationQuestionId, categoryId, 3, 4, "Prioritisation question", true]]),
                new FakeResultSet(["Id", "Title", "Other", "StatusName"], [[profileId, "Profile 1", null, "Published"]]),
                new FakeResultSet(
                    ["Id", "ProfileId", "State", "Major", "Minor", "Name", "Flag"],
                    [[versionId, profileId, "Published", (byte)1, (byte)2, "Version", true]]),
                new FakeResultSet(["Id", "ProfileVersionId", "Name"], [[speciesId, versionId, "Cattle"]]),
                new FakeResultSet(["QuestionId", "ProfileVersionId", "Answer"], [[questionId, versionId, "Yes"]]),
                new FakeResultSet(
                    ["QuestionId", "ProfileVersionId", "ValueId", "Value", "Score", "Weight"],
                    [[prioritisationQuestionId, versionId, answerId, "High", 5, 2]]),
                new FakeResultSet(
                    ["ProfileVersionId", "CategoryId", "Other", "Score", "Rank"],
                    [[versionId, categoryId, Guid.NewGuid(), 10, 1]])
            ]
        });

        var data = await CreateRepository().GetScoreDataAsync(CancellationToken.None);

        data.CommonQuestions.Should().ContainSingle().Which.DisplayQuestionNumber.Should().Be("1.2");
        data.Categories.Should().ContainSingle().Which.Name.Should().Be("Welfare");
        data.PrioritisationQuestions.Should().ContainSingle().Which.DisplayQuestionNumber.Should().Be("3.4");
        data.Profiles.Should().ContainSingle().Which.StatusName.Should().Be("Published");
        data.ProfileVersions.Should().ContainSingle().Which.DisplayVersionNumber.Should().Be("v1.2");
        data.ProfiledSpecies.Should().ContainSingle();
        data.CommonAnswers.Should().ContainSingle();
        data.PrioritisationAnswers.Should().ContainSingle();
        data.NormalisedScores.Should().ContainSingle();
    }

    [Fact]
    public async Task GetScoreDataAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetScoreData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetScoreDataAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
