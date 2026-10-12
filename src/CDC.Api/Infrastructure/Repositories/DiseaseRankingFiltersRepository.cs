using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRankingFilters;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IDiseaseRankingFiltersRepository"/>, backed by the legacy
/// <c>DiseaseFilters</c> table (one row per selected species per filter).
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class DiseaseRankingFiltersRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<DiseaseRankingFiltersRepository> logger)
    : IDiseaseRankingFiltersRepository
{
    /// <summary><c>Disease5</c> is stored as a comma-separated list of selected types.</summary>
    private const char DiseaseTypesSeparator = ',';

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiseaseRankingFilterSummary>> GetFiltersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<FilterSummaryRow>(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.GetFilters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var filters = rows
                .Select(row => new DiseaseRankingFilterSummary
                {
                    FilterNumber = row.FilterNumber,
                    DiseaseName = row.DiseaseName ?? string.Empty
                })
                .ToList();

            logger.RetrievedFilters(filters.Count);

            return filters;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.GetFilters);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DiseaseRankingFilterDetail?> GetFilterDetailAsync(long filterNumber, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = (await connection.QueryAsync<FilterDetailRow>(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.GetFilterDetail,
                new { FilterNumber = filterNumber },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
            {
                return null;
            }

            var first = rows[0];

            return new DiseaseRankingFilterDetail
            {
                FilterNumber = first.FilterNumber,
                DiseaseName = first.DiseaseName ?? string.Empty,
                Disease1 = first.Disease1 ?? string.Empty,
                Disease2 = first.Disease2 ?? string.Empty,
                Disease3 = first.Disease3 ?? string.Empty,
                Disease4 = first.Disease4 ?? string.Empty,
                DiseaseTypes = SplitDiseaseTypes(first.Disease5),
                SpeciesIds = [.. rows.Select(row => row.SelectedSpecies).Distinct()]
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.GetFilterDetail);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> ReserveFilterNumberAsync(string diseaseName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            return await connection.QuerySingleAsync<long>(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.ReserveFilterNumber,
                new { DiseaseName = diseaseName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.ReserveFilterNumber);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InsertFilterSpeciesAsync(
        long filterNumber,
        SaveDiseaseRankingFilterRequestDto request,
        Guid speciesId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.InsertFilterValue,
                BuildFilterValueParameters(filterNumber, request, speciesId),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.InsertFilterValue);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateFilterSpeciesAsync(
        long filterNumber,
        SaveDiseaseRankingFilterRequestDto request,
        Guid speciesId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.UpdateFilterValue,
                BuildFilterValueParameters(filterNumber, request, speciesId),
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.UpdateFilterValue);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteFilterAsync(long filterNumber, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                DiseaseRankingFiltersStoredProcedures.DisableFilterValue,
                new { FilterNumber = filterNumber },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, DiseaseRankingFiltersStoredProcedures.DisableFilterValue);
            throw;
        }
    }

    /// <summary><c>spiDiseaseFilterValue</c> and <c>spiUpdateDiseaseFilterValue</c> share the same parameter shape.</summary>
    private static object BuildFilterValueParameters(long filterNumber, SaveDiseaseRankingFilterRequestDto request, Guid speciesId) => new
    {
        FilterNumber = filterNumber,
        DiseaseName = request.DiseaseName,
        request.Disease1,
        request.Disease2,
        request.Disease3,
        request.Disease4,
        Disease5 = string.Join(DiseaseTypesSeparator, request.DiseaseTypes),
        SelectedSpecies = speciesId
    };

    private static string[] SplitDiseaseTypes(string? disease5) =>
        string.IsNullOrWhiteSpace(disease5) || disease5 == "NULL"
            ? []
            : disease5.Split(DiseaseTypesSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(DiseaseRankingFiltersRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        await dbConnection.OpenAsync(cancellationToken);
        return dbConnection;
    }

    private sealed record FilterSummaryRow(long FilterNumber, string? DiseaseName);

    private sealed record FilterDetailRow(
        long FilterNumber,
        string? DiseaseName,
        string? Disease1,
        string? Disease2,
        string? Disease3,
        string? Disease4,
        string? Disease5,
        Guid SelectedSpecies);
}
