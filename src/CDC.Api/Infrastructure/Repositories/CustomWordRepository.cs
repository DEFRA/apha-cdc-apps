using System.Data;
using System.Data.Common;
using CDC.Api.Features.SpellCheck;
using CDC.Api.Features.SpellCheck.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="ICustomWordRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class CustomWordRepository(IDbConnectionFactory connectionFactory, ILogger<CustomWordRepository> logger)
    : ICustomWordRepository
{
    private const int RowVersionLength = 8;
    private const int WordColumnLength = 255;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    SpellCheckStoredProcedures.GetCustomWords,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

            var words = new List<string>();

            // Columns: Id, Word, LastUpdated.
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(1))
                {
                    words.Add(reader.GetString(1));
                }
            }

            return words;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpellCheckStoredProcedures.GetCustomWords);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddCustomWordAsync(Guid id, string word, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", id, DbType.Guid);
            parameters.Add("@Word", word, DbType.String, size: WordColumnLength);
            // The procedure outputs the new row version, which nothing here needs but which it requires to be passed.
            parameters.Add("@LastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                SpellCheckStoredProcedures.InsertCustomWord,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpellCheckStoredProcedures.InsertCustomWord);
            throw;
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(CustomWordRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }
}
