using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IDiseaseRankingRepository"/>. The procedures return
/// positional columns, so they are read through a data reader in the same column order as the
/// legacy <c>PrioritisationService.GetRankProfileDataForFilter</c>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class DiseaseRankingRepository(IDbConnectionFactory connectionFactory, ILogger<DiseaseRankingRepository> logger)
    : IDiseaseRankingRepository
{
    private const string PublicHealthCategory = "public health";
    private const string WelfareCategory = "welfare";
    private const string WiderSocietyCategory = "wider society";
    private const string InternationalTradeCategory = "international trade";
    private const string RiskCategory = "post mitigation risk";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiseaseRankingFilter>> GetFiltersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                DiseaseRankingStoredProcedures.GetFilterNames,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var filters = new List<DiseaseRankingFilter>();

            // Columns: FilterId, DiseaseName, DateInserted.
            while (await reader.ReadAsync(cancellationToken))
            {
                filters.Add(new DiseaseRankingFilter(reader.GetGuid(0), reader.ReadString(1)));
            }

            return filters;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingStoredProcedures.GetFilterNames);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RankedProfile>> GetRankedProfilesAsync(string? filterName, CancellationToken cancellationToken)
    {
        var storedProcedure = filterName is null
            ? DiseaseRankingStoredProcedures.GetRankProfileData
            : DiseaseRankingStoredProcedures.GetFilterRankProfileData;

        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                storedProcedure,
                filterName is null ? null : new { DiseaseName = filterName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // One row per profile, species and category: fold them into one entry per profile and species.
            var byKey = new Dictionary<(Guid ProfileId, Guid SpeciesId), RankedProfile>();

            // Columns: ProfileId, SpeciesId, Category, Rank, IsEndemic, Title, SpeciesName,
            // ProfileVersionState, IsValidationComplete, ProfileVersionId.
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = (reader.GetGuid(0), reader.GetGuid(1));
                var current = byKey.GetValueOrDefault(key) ?? CreateProfile(reader);

                byKey[key] = ApplyRank(current, reader.ReadString(2), reader.ReadInt32(3));
            }

            return [.. byKey.Values];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    private static RankedProfile CreateProfile(DbDataReader reader)
    {
        var isPublished = reader.ReadString(7) == "Published";
        var isValidationComplete = reader.ReadBoolean(8);

        return new RankedProfile
        {
            ProfileId = reader.GetGuid(0),
            SpeciesId = reader.GetGuid(1),
            ProfileTitle = reader.ReadString(5),
            SpeciesName = reader.ReadString(6),
            IsExotic = !reader.ReadBoolean(4),
            IsAwaitingValidation = !isPublished && !isValidationComplete
        };
    }

    private static RankedProfile ApplyRank(RankedProfile profile, string category, int rank) =>
        category.ToLowerInvariant() switch
        {
            InternationalTradeCategory => profile with { InternationalTradeRank = rank },
            PublicHealthCategory => profile with { PublicHealthRank = rank },
            RiskCategory => profile with { RiskRank = rank },
            WelfareCategory => profile with { AnimalWelfareRank = rank },
            WiderSocietyCategory => profile with { WiderSocietyRank = rank },
            _ => profile
        };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(DiseaseRankingRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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
