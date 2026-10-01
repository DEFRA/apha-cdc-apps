using System.Data.Common;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesRepositoryTests : IDisposable
{
    private static readonly Guid RankingRangeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

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
    public async Task GetCategoriesWithCriteriaAsync_BuildsTheCategoryGraph()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["Id", "LowerBound", "UpperBound", "LastUpdated"], [[RankingRangeId, 0, 100, new byte[8]]]),
                new FakeResultSet(["Id", "Name"], [[CategoryId, "Animal welfare"]]),
                new FakeResultSet(["Id", "PrioritisationCategoryId", "Code", "Name", "Weighting"], [[CriterionId, CategoryId, "C1", "Impact", 10]]),
                new FakeResultSet(["Id", "PrioritisationCriterionId", "CriterionValue", "Score"], [[ValueId, CriterionId, "N/A", 5]])
            ]
        });

        var categories = await CreateRepository().GetCategoriesWithCriteriaAsync(CancellationToken.None);

        var category = categories.Should().ContainSingle().Subject;
        category.Name.Should().Be("Animal welfare");
        var criterion = category.Criteria.Should().ContainSingle().Subject;
        criterion.Code.Should().Be("C1");
        var value = criterion.Values.Should().ContainSingle().Subject;
        value.Value.Should().Be("N/A");
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
    public async Task UpdateCriterionAsync_PassesParameters()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterion, new FakeCommandScript());

        await CreateRepository().UpdateCriterionAsync(CriterionId, "Impact", 42, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("Weighting").WhoseValue.Should().Be(42);
    }

    [Fact]
    public async Task UpdateCriterionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateCriterionAsync(CriterionId, "Impact", 42, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateCriterionValueScoreAsync_PassesParameters()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterionValue, new FakeCommandScript());

        await CreateRepository().UpdateCriterionValueScoreAsync(ValueId, 7, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("Score").WhoseValue.Should().Be(7);
    }

    [Fact]
    public async Task UpdateCriterionValueScoreAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateCriterionValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateCriterionValueScoreAsync(ValueId, 7, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRankingRangeAsync_MapsTheCurrentRow()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.GetAll, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "LowerBound", "UpperBound", "LastUpdated"], [[RankingRangeId, 20, 80, new byte[8]]])]
        });

        var rankingRange = await CreateRepository().GetRankingRangeAsync(CancellationToken.None);

        rankingRange.LowerBound.Should().Be(20);
        rankingRange.UpperBound.Should().Be(80);
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
        byte[] newRowVersion = [9, 9, 9, 9, 9, 9, 9, 9];
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateRankingRange, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = newRowVersion }
        });

        var result = await CreateRepository().UpdateRankingRangeAsync(10, 90, new byte[8], CancellationToken.None);

        result.Should().Equal(newRowVersion);
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_ThrowsConcurrencyException_WhenRowVersionHasMovedOn()
    {
        connection.Script(
            PrioritisationVariablesStoredProcedures.UpdateRankingRange,
            new FakeCommandScript { Throws = new FakeDbException("The row has been edited by another user.") });

        var act = () => CreateRepository().UpdateRankingRangeAsync(10, 90, new byte[8], CancellationToken.None);

        await act.Should().ThrowAsync<CDC.Api.Domain.Exceptions.ConcurrencyException>();
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_RethrowsAndLogs_OnUnrelatedDbException()
    {
        connection.Script(PrioritisationVariablesStoredProcedures.UpdateRankingRange, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateRankingRangeAsync(10, 90, new byte[8], CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRankingRangeAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new PrioritisationVariablesRepository(new StubConnectionFactory(new NotADbConnection()), logger.Object);

        var act = () => repository.GetRankingRangeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRankingRangeAsync_RethrowsAndLogs_WhenOpenAsyncFails()
    {
        var repository = new PrioritisationVariablesRepository(new StubConnectionFactory(new ThrowingOpenDbConnection()), logger.Object);

        var act = () => repository.GetRankingRangeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(System.Data.IDbConnection connection) : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => connection;
    }

    /// <summary>A bare <see cref="System.Data.IDbConnection"/> (not a <see cref="DbConnection"/>) to exercise the repository's defensive type check.</summary>
    private sealed class NotADbConnection : System.Data.IDbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string ConnectionString { get; set; } = string.Empty;

        public int ConnectionTimeout => 0;

        public string Database => string.Empty;

        public System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public System.Data.IDbTransaction BeginTransaction() => throw new NotSupportedException();

        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();

        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public void Close()
        {
        }

        public System.Data.IDbCommand CreateCommand() => throw new NotSupportedException();

        public void Open()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A <see cref="DbConnection"/> whose <see cref="Open"/> always fails, to exercise the open-connection failure path.</summary>
    private sealed class ThrowingOpenDbConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close()
        {
        }

        public override void Open() => throw new FakeDbException("connect failed");

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
