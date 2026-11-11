using System.Data;
using System.Data.Common;
using System.Globalization;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.PrioritisationVariables;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IPrioritisationVariablesRepository"/>.
/// </summary>
/// <remarks>
/// <c>spgaPrioritisationVariables</c> returns four result sets: ranking range, categories,
/// criteria and criterion values, combined into the returned category/criterion/value graph
/// (or read alone by <see cref="GetRankingRangeAsync"/>, since Dapper's simple query methods
/// only ever read the first result set).
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class PrioritisationVariablesRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<PrioritisationVariablesRepository> logger)
    : IPrioritisationVariablesRepository
{
    /// <summary>SQL Server reports <c>RAISERROR</c> with a user-defined message as error 50000.</summary>
    private const int UserRaisedErrorNumber = 50000;

    /// <summary>The message <c>spuPrioritisationRankingRange</c> raises when the row version has moved on.</summary>
    private const string ConcurrencyMessageFragment = "edited by another user";

    private const int RowVersionLength = 8;

    /// <summary>The only <c>PrioritisationType</c> this page maintains; species has its own equivalent page.</summary>
    private const string ProfilePrioritisationType = "Profile";

    private const string DraftProfileVersionState = "Draft";

    /// <summary>Matches the legacy three-minute <c>TransactionScope</c> around the recalculation.</summary>
    private const int RecalculationTimeoutSeconds = 180;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PrioritisationCategory>> GetCategoriesWithCriteriaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    PrioritisationVariablesStoredProcedures.GetAll,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: ranking range - read separately by GetRankingRangeAsync.
            await reader.NextResultAsync(cancellationToken);

            var (categoryNames, orderedCategoryIds) = await ReadCategoryNamesAsync(reader, cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var (criterionRows, criterionIdsByCategory) = await ReadCriterionRowsAsync(reader, cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var valuesByCriterion = await ReadCriterionValuesAsync(reader, cancellationToken);

            var categories = BuildCategories(
                orderedCategoryIds,
                categoryNames,
                criterionIdsByCategory,
                criterionRows,
                valuesByCriterion);

            logger.RetrievedCategories(categories.Count);

            return categories;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.GetAll);
            throw;
        }
    }

    /// <summary>Reads result set 2: categories (Id, Name).</summary>
    private static async Task<(Dictionary<Guid, string> Names, List<Guid> OrderedIds)> ReadCategoryNamesAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var categoryNames = new Dictionary<Guid, string>();
        var orderedCategoryIds = new List<Guid>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            categoryNames[id] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            orderedCategoryIds.Add(id);
        }

        return (categoryNames, orderedCategoryIds);
    }

    /// <summary>Reads result set 3: criteria (Id, PrioritisationCategoryId, Code, Name, Weighting), in code order.</summary>
    private static async Task<(
        List<(Guid Id, Guid CategoryId, string Code, string Name, int Weight)> Rows,
        Dictionary<Guid, List<Guid>> IdsByCategory)> ReadCriterionRowsAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var criterionRows = new List<(Guid Id, Guid CategoryId, string Code, string Name, int Weight)>();
        var criterionIdsByCategory = new Dictionary<Guid, List<Guid>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var categoryId = reader.GetGuid(1);
            var criterionId = reader.GetGuid(0);

            criterionRows.Add((
                criterionId,
                categoryId,
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                reader.GetInt32(4)));

            if (!criterionIdsByCategory.TryGetValue(categoryId, out var criterionIds))
            {
                criterionIds = [];
                criterionIdsByCategory[categoryId] = criterionIds;
            }

            criterionIds.Add(criterionId);
        }

        return (criterionRows, criterionIdsByCategory);
    }

    /// <summary>Reads result set 4: criterion values (Id, PrioritisationCriterionId, CriterionValue, Score), in sequence order.</summary>
    private static async Task<Dictionary<Guid, List<PrioritisationCriterionValue>>> ReadCriterionValuesAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var valuesByCriterion = new Dictionary<Guid, List<PrioritisationCriterionValue>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var criterionId = reader.GetGuid(1);
            var value = new PrioritisationCriterionValue
            {
                Id = reader.GetGuid(0),
                CriterionId = criterionId,
                Value = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Score = reader.GetInt32(3)
            };

            if (!valuesByCriterion.TryGetValue(criterionId, out var values))
            {
                values = [];
                valuesByCriterion[criterionId] = values;
            }

            values.Add(value);
        }

        return valuesByCriterion;
    }

    /// <summary>Assembles the category/criterion/value graph from the three result sets read above.</summary>
    private static List<PrioritisationCategory> BuildCategories(
        List<Guid> orderedCategoryIds,
        Dictionary<Guid, string> categoryNames,
        Dictionary<Guid, List<Guid>> criterionIdsByCategory,
        List<(Guid Id, Guid CategoryId, string Code, string Name, int Weight)> criterionRows,
        Dictionary<Guid, List<PrioritisationCriterionValue>> valuesByCriterion)
    {
        var criteriaById = criterionRows.ToDictionary(
            row => row.Id,
            row => new PrioritisationCriterion
            {
                Id = row.Id,
                CategoryId = row.CategoryId,
                Code = row.Code,
                Name = row.Name,
                Weight = row.Weight,
                Values = valuesByCriterion.TryGetValue(row.Id, out var values) ? values : []
            });

        return [.. orderedCategoryIds.Select(id => new PrioritisationCategory
        {
            Id = id,
            Name = categoryNames[id],
            Criteria = criterionIdsByCategory.TryGetValue(id, out var criterionIds)
                ? [.. criterionIds.Select(criterionId => criteriaById[criterionId])]
                : []
        })];
    }


    /// <inheritdoc />
    public async Task UpdateCriterionAsync(Guid criterionId, string name, int weight, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            // spuPrioritisationCriterion runs with SET NOCOUNT ON, so the rows-affected count
            // ExecuteAsync would return is unreliable (-1); existence is already confirmed by
            // the caller reading the graph before invoking this.
            await connection.ExecuteAsync(new CommandDefinition(
                PrioritisationVariablesStoredProcedures.UpdateCriterion,
                new { CriterionId = criterionId, Weighting = weight, Name = name },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.UpdateCriterion);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateCriterionValueScoreAsync(Guid criterionValueId, int score, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                PrioritisationVariablesStoredProcedures.UpdateCriterionValue,
                new { CriterionValueId = criterionValueId, Score = score },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.UpdateCriterionValue);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PrioritisationRankingRange> GetRankingRangeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            // Dapper's simple query methods only ever read the first result set, which is the
            // ranking range - the categories/criteria/values that follow are simply not read.
            var row = await connection.QuerySingleAsync<RankingRangeRow>(new CommandDefinition(
                PrioritisationVariablesStoredProcedures.GetAll,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return new PrioritisationRankingRange
            {
                Id = row.Id,
                LowerBound = row.LowerBound,
                UpperBound = row.UpperBound,
                RowVersion = row.LastUpdated
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.GetAll);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> UpdateRankingRangeAsync(int lowerBound, int upperBound, byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UpperBound", upperBound, DbType.Int32);
        parameters.Add("@LowerBound", lowerBound, DbType.Int32);
        parameters.Add("@LastUpdated", rowVersion, DbType.Binary, size: RowVersionLength);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);
        parameters.Add("@PrioritisationType", ProfilePrioritisationType, DbType.AnsiString, size: 20);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                PrioritisationVariablesStoredProcedures.UpdateRankingRange,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.UpdateRankingRange);

            // spuPrioritisationRankingRange RAISERRORs (error 50000) when @LastUpdated is stale.
            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(exception.Message, exception)
                : exception;
        }

        return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    /// <inheritdoc />
    public async Task<int> PrioritiseDraftProfilesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var storedProcedure = PrioritisationVariablesStoredProcedures.GetCurrentProfiles;

        try
        {
            var profileVersionIds = new List<Guid>();

            await using (var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                storedProcedure,
                new { ProfileVersionState = new DbString { Value = DraftProfileVersionState, IsAnsi = true, Length = 20 } },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    profileVersionIds.Add(reader.GetGuid(0));
                }
            }

            storedProcedure = PrioritisationVariablesStoredProcedures.CalculatePrioritisation;

            foreach (var profileVersionId in profileVersionIds)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    storedProcedure,
                    new { ProfileVersionId = profileVersionId },
                    transaction,
                    RecalculationTimeoutSeconds,
                    CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            storedProcedure = PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore;
            await connection.ExecuteAsync(new CommandDefinition(
                storedProcedure,
                transaction: transaction,
                commandTimeout: RecalculationTimeoutSeconds,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return profileVersionIds.Count;
        }
        catch (DbException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RecalculatePrioritisationScoresAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore,
                commandTimeout: RecalculationTimeoutSeconds,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.CalculatePrioritisationScore);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PrioritisationScoreData> GetScoreDataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    PrioritisationVariablesStoredProcedures.GetScoreData,
                    commandTimeout: RecalculationTimeoutSeconds,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var commonQuestions = await ReadRowsAsync(reader, row => new ScoreDataCommonQuestion(
                row.GetGuid(0), FormatQuestionNumber(row, 1, 2), row.ReadString(3)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var categories = await ReadRowsAsync(reader, row => new ScoreDataCategory(row.GetGuid(0), row.ReadString(2)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var prioritisationQuestions = await ReadRowsAsync(reader, row => new ScoreDataPrioritisationQuestion(
                row.GetGuid(0), row.GetGuid(1), FormatQuestionNumber(row, 2, 3), row.ReadString(4), row.ReadBoolean(5)), cancellationToken);

            // Older database builds return only Id and Title; the deployed procedure adds StatusId and StatusName.
            await reader.NextResultAsync(cancellationToken);
            var profiles = await ReadRowsAsync(reader, row => new ScoreDataProfile(
                row.GetGuid(0), row.ReadString(1), row.FieldCount > 3 ? row.ReadString(3) : string.Empty), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var profileVersions = await ReadRowsAsync(reader, row => new ScoreDataProfileVersion(
                row.GetGuid(0),
                row.GetGuid(1),
                row.ReadString(2),
                $"v{ReadNumber(row, 3)}.{ReadNumber(row, 4)}",
                row.ReadString(5),
                row.ReadBoolean(6)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var profiledSpecies = await ReadRowsAsync(reader, row => new ScoreDataProfiledSpecies(
                row.GetGuid(0), row.GetGuid(1), row.ReadString(2)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var commonAnswers = await ReadRowsAsync(reader, row => new ScoreDataCommonAnswer(
                row.GetGuid(0), row.GetGuid(1), row.ReadString(2)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var prioritisationAnswers = await ReadRowsAsync(reader, row => new ScoreDataPrioritisationAnswer(
                row.GetGuid(0), row.GetGuid(1), row.ReadGuid(2), row.ReadString(3), ReadNumber(row, 4), ReadNumber(row, 5)), cancellationToken);

            await reader.NextResultAsync(cancellationToken);
            var normalisedScores = await ReadRowsAsync(reader, row => new ScoreDataNormalisedScore(
                row.GetGuid(0), row.GetGuid(1), row.GetGuid(2), ReadNumber(row, 3), ReadNumber(row, 4)), cancellationToken);

            return new PrioritisationScoreData
            {
                CommonQuestions = commonQuestions,
                Categories = categories,
                PrioritisationQuestions = prioritisationQuestions,
                Profiles = profiles,
                ProfileVersions = profileVersions,
                ProfiledSpecies = profiledSpecies,
                CommonAnswers = commonAnswers,
                PrioritisationAnswers = prioritisationAnswers,
                NormalisedScores = normalisedScores
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.GetScoreData);
            throw;
        }
    }

    private static async Task<List<T>> ReadRowsAsync<T>(DbDataReader reader, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        var rows = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    // Version numbers are tinyint and section/question numbers int, so read them through the boxed value.
    private static int ReadNumber(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string FormatQuestionNumber(DbDataReader reader, int sectionOrdinal, int questionOrdinal) =>
        $"{ReadNumber(reader, sectionOrdinal)}.{ReadNumber(reader, questionOrdinal)}";

    /// <summary>
    /// Identifies the row version clash raised by <c>spuPrioritisationRankingRange</c>. The
    /// message is also matched so that providers other than SQL Server - including the fakes
    /// used in tests - can signal the same condition.
    /// </summary>
    /// <param name="exception">The database exception to classify.</param>
    /// <returns><see langword="true"/> when the failure is a concurrent edit.</returns>
    private static bool IsConcurrencyViolation(DbException exception) =>
        exception is SqlException { Number: UserRaisedErrorNumber } ||
        exception.Message.Contains(ConcurrencyMessageFragment, StringComparison.OrdinalIgnoreCase);

    private sealed record RankingRangeRow(Guid Id, int LowerBound, int UpperBound, byte[] LastUpdated);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(PrioritisationVariablesRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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
