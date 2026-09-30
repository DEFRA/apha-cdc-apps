using System.Data;
using System.Data.Common;
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

            // Result set 2: categories (Id, Name).
            var categoryNames = new Dictionary<Guid, string>();
            var orderedCategoryIds = new List<Guid>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                categoryNames[id] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                orderedCategoryIds.Add(id);
            }

            // Result set 3: criteria (Id, PrioritisationCategoryId, Code, Name, Weighting), in code order.
            await reader.NextResultAsync(cancellationToken);

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

            // Result set 4: criterion values (Id, PrioritisationCriterionId, CriterionValue, Score), in sequence order.
            await reader.NextResultAsync(cancellationToken);

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

            var categories = orderedCategoryIds.Select(id => new PrioritisationCategory
            {
                Id = id,
                Name = categoryNames[id],
                Criteria = criterionIdsByCategory.TryGetValue(id, out var criterionIds)
                    ? [.. criterionIds.Select(criterionId => criteriaById[criterionId])]
                    : []
            }).ToList();

            logger.RetrievedCategories(categories.Count);

            return categories;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, PrioritisationVariablesStoredProcedures.GetAll);
            throw;
        }
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
