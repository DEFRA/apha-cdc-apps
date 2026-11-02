using System.Data;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.SpellCheck;

public sealed class CustomWordRepositoryTests : IDisposable
{
    private static readonly Guid WordId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string[] WordColumns = ["Id", "Word", "LastUpdated"];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<CustomWordRepository>> logger = new();

    public CustomWordRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private CustomWordRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetCustomWordsAsync_ReadsTheWordOfEveryRow_AndSkipsARowWithNoWord()
    {
        connection.Script(SpellCheckStoredProcedures.GetCustomWords, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    WordColumns,
                    [
                        [WordId, "Defra", new byte[8]],
                        [Guid.NewGuid(), null, new byte[8]],
                        [Guid.NewGuid(), "APHA", new byte[8]]
                    ])
            ]
        });

        var words = await CreateRepository().GetCustomWordsAsync(CancellationToken.None);

        words.Should().Equal("Defra", "APHA");
        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(SpellCheckStoredProcedures.GetCustomWords);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetCustomWordsAsync_ReturnsEmpty_WhenTheDictionaryHasNoWords()
    {
        connection.Script(SpellCheckStoredProcedures.GetCustomWords, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(WordColumns)]
        });

        var words = await CreateRepository().GetCustomWordsAsync(CancellationToken.None);

        words.Should().BeEmpty();
    }

    [Fact]
    public async Task AddCustomWordAsync_CallsTheInsertProcedureWithTheIdAndTheWord_AndAnOutputForTheRowVersion()
    {
        connection.Script(SpellCheckStoredProcedures.InsertCustomWord, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["LastUpdated"] = new byte[8] }
        });

        await CreateRepository().AddCustomWordAsync(WordId, "Defra", CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(SpellCheckStoredProcedures.InsertCustomWord);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(WordId);
        executed.Parameters.Should().ContainKey("Word").WhoseValue.Should().Be("Defra");
        executed.Parameters.Should().ContainKey("LastUpdated");
    }

    [Fact]
    public async Task GetCustomWordsAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script(SpellCheckStoredProcedures.GetCustomWords, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCustomWordsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task AddCustomWordAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script(SpellCheckStoredProcedures.InsertCustomWord, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().AddCustomWordAsync(WordId, "Defra", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetCustomWordsAsync_Rethrows_WhenTheConnectionCannotBeOpened()
    {
        connection.ThrowOnOpen = new FakeDbException("cannot open");

        var act = () => CreateRepository().GetCustomWordsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetCustomWordsAsync_Throws_WhenTheFactoryDoesNotReturnADbConnection()
    {
        var nonDbConnection = new Mock<IDbConnection>();
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateConnection()).Returns(nonDbConnection.Object);
        var repository = new CustomWordRepository(factory.Object, logger.Object);

        var act = () => repository.GetCustomWordsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
