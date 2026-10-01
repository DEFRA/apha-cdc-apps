using System.Data;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public class ReferenceDataRepositoryTests : IDisposable
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

    private IReferenceDataRepository CreateRepository() =>
        new ReferenceDataRepository(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetReferenceValuesAsync_ExecutesStoredProcedureAndMapsRows()
    {
        connection.Script(ReferenceDataStoredProcedures.GetLookupValuesByTable, new FakeCommandScript
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
        executed.CommandText.Should().Be(ReferenceDataStoredProcedures.GetLookupValuesByTable);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_ReturnsEmpty_WhenTheTableHasNoValues()
    {
        connection.Script(ReferenceDataStoredProcedures.GetLookupValuesByTable, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "LookupValue"], [])]
        });

        var values = await CreateRepository().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        values.Should().BeEmpty();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
