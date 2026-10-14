namespace CDC.Api.Infrastructure.Repositories;

/// <summary>Names of the Surveillance Profiles stored procedures backing disease ranking reports.</summary>
public static class DiseaseRankingStoredProcedures
{
    /// <summary>Reads the saved disease ranking filter names.</summary>
    public const string GetFilterNames = "spiGetDiseaseRankingFilterName";

    /// <summary>Reads the rank data for every profile.</summary>
    public const string GetRankProfileData = "spgaRankProfileData";

    /// <summary>Reads the rank data for the profiles matching a saved filter.</summary>
    public const string GetFilterRankProfileData = "spgaFilterRankProfileData";
}
