/****** New stored procedure for the disease ranking filter feature: lists every active filter
        (one row per FilterNumber) for the "Select Filter" dropdown on EditFilters.cshtml. The
        legacy DiseaseFilters table stores one row per species per filter, so DiseaseName is
        aggregated to collapse that to one row per filter. ******/
CREATE OR ALTER PROCEDURE [dbo].[spgDiseaseRankingFilters]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[FilterNumber],
		MAX([DiseaseName]) AS [DiseaseName]
	FROM [dbo].[DiseaseFilters]
	WHERE [IsActive] = 1 AND [IsInUse] = 1
	GROUP BY [FilterNumber]
	ORDER BY MAX([DiseaseName])
END

GO
