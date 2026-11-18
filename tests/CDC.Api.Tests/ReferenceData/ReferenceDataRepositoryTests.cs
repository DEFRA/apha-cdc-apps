using System.Data;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public sealed class ReferenceDataRepositoryTests : IDisposable
{
    private static readonly Guid ReferenceTableId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OptionId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ReferenceDataRepository>> logger = new();

    public ReferenceDataRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ReferenceDataRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetReferenceValuesAsync_ExecutesTheOrderedQueryAndMapsRows()
    {
        connection.Script(ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "LookupValue"],
                    [[OptionId, "Market records"]])
            ]
        });

        var values = await CreateRepository().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        var value = values.Should().ContainSingle().Subject;
        value.Id.Should().Be(OptionId);
        value.Value.Should().Be("Market records");

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable);
        executed.CommandType.Should().Be(CommandType.Text);
        executed.Parameters.Should().ContainKey("ReferenceTableId").WhoseValue.Should().Be(ReferenceTableId);
    }

    // The legacy list controls put a maintainable table's value with no sequence number ("Currently not known") after the others, which
    // spgLookupValueByTable, sorting by sequence number alone, does not.
    [Fact]
    public void TheOrderedQuery_SortsAValueWithNoSequenceNumberAfterTheOthers_ThenByWhenItTookEffect()
    {
        ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable.Should()
            .EndWith("ORDER BY ISNULL([SequenceNumber], 255), [EffectiveDateFrom]");
    }

    [Fact]
    public async Task GetReferenceValuesAsync_ReturnsEmpty_WhenTheTableHasNoValues()
    {
        connection.Script(ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "LookupValue"], [])]
        });

        var values = await CreateRepository().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReferenceValuesAsync_DefaultsNullLookupValue()
    {
        connection.Script(ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "LookupValue"],
                    [[OptionId, null]])
            ]
        });

        var values = await CreateRepository().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        values.Should().ContainSingle().Which.Value.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ReferenceDataStoredProcedures.GetOrderedLookupValuesByTable, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetReferenceValuesAsync_Throws_WhenFactoryDoesNotReturnADbConnection()
    {
        var repository = new ReferenceDataRepository(new NonDbConnectionFactory(), logger.Object);

        var act = () => repository.GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

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
