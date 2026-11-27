using System.Data;
using System.Data.Common;
using CDC.Api.Features.CrossCuttingIssueScores;
using CDC.Api.Features.CrossCuttingIssueScores.Dtos;
using CDC.Api.Features.CrossCuttingIssueScores.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="ICrossCuttingIssueScoresRepository"/>.
/// </summary>
/// <remarks>
/// <c>spgaSpeciesPrioritisationVariables</c> returns four result sets: last updated, categories,
/// criteria and criterion values.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class CrossCuttingIssueScoresRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<CrossCuttingIssueScoresRepository> logger)
    : ICrossCuttingIssueScoresRepository
{
    private const string GetAll = "spgaSpeciesPrioritisationVariables";
    private const string UpdateValue = "spuSpeciesPrioritisationCriterionValue";
    private const string Recalculate = "sppSpeciesPrioritisationScore";

    /// <summary>Matches the legacy three-minute <c>TransactionScope</c> around the recalculation.</summary>
    private const int RecalculationTimeoutSeconds = 180;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrossCuttingIssueCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(GetAll, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: last updated - not needed here.
            await reader.NextResultAsync(cancellationToken);

            var categories = new List<(Guid Id, string Name)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                categories.Add((reader.GetGuid(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }

            await reader.NextResultAsync(cancellationToken);
            var criteria = new List<(Guid Id, Guid CategoryId, string Name)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                criteria.Add((reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
            }

            await reader.NextResultAsync(cancellationToken);
            var values = new List<(Guid Id, Guid CriterionId, string Value, int Score)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture)));
            }

            var valuesByCriterion = values.ToLookup(value => value.CriterionId);
            var criteriaByCategory = criteria.ToLookup(criterion => criterion.CategoryId);

            // The procedure orders every set by SequenceNumber without returning it, so the order read is the sequence.
            IReadOnlyList<CrossCuttingIssueCategoryDto> result =
            [
                .. categories.Select((category, categoryIndex) => new CrossCuttingIssueCategoryDto(
                    category.Id,
                    category.Name,
                    categoryIndex + 1,
                    [
                        .. criteriaByCategory[category.Id].Select((criterion, criterionIndex) => new CrossCuttingIssueCriterionDto(
                            criterion.Id,
                            criterion.Name,
                            criterionIndex + 1,
                            [
                                .. valuesByCriterion[criterion.Id].Select((value, valueIndex) => new CrossCuttingIssueCriterionValueDto(
                                    value.Id,
                                    value.Value,
                                    value.Score,
                                    valueIndex + 1))
                            ]))
                    ]))
            ];

            logger.RetrievedCategories(result.Count);

            return result;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, GetAll);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateScoresAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var storedProcedure = UpdateValue;

        try
        {
            foreach (var (id, score) in scores)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    storedProcedure,
                    new { Id = id, Score = score },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            storedProcedure = Recalculate;
            await connection.ExecuteAsync(new CommandDefinition(
                storedProcedure,
                transaction: transaction,
                commandTimeout: RecalculationTimeoutSeconds,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            logger.StoredProcedureFailed(exception, storedProcedure);
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
                $"{nameof(CrossCuttingIssueScoresRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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
