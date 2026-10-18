namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the disease ranking filters
/// feature. <see cref="GetFilters"/> and <see cref="GetFilterDetail"/> are new (see
/// <c>CDC.Database/Scripts</c>); the rest are the legacy procedures, reused as-is.
/// </summary>
public static class DiseaseRankingFiltersStoredProcedures
{
    /// <summary>Returns every active filter, one row per <c>FilterNumber</c>.</summary>
    public const string GetFilters = "spgDiseaseRankingFilters";

    /// <summary>Returns every active species row for one filter.</summary>
    public const string GetFilterDetail = "spgDiseaseRankingFilterDetail";

    /// <summary>Reserves the next filter number for a name, or returns 0 if the name is already in use.</summary>
    public const string ReserveFilterNumber = "spiGetMaxFilterNumber";

    /// <summary>Inserts one species row for a new filter.</summary>
    public const string InsertFilterValue = "spiDiseaseFilterValue";

    /// <summary>Updates (or re-inserts) one species row for an existing filter.</summary>
    public const string UpdateFilterValue = "spiUpdateDiseaseFilterValue";

    /// <summary>Soft-deletes every row for a filter.</summary>
    public const string DisableFilterValue = "spiUpdateDisableDiseaseFilterValue";
}
