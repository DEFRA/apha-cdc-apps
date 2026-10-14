/****** New stored procedure for the disease ranking filter feature: returns every species row
        recorded for one filter, used to re-display a selected filter on EditFilters.cshtml.
        Disease1-4 and Disease5 (a comma-separated list of disease types) are identical on every
        row for a given FilterNumber, so the caller reads them from the first row and collects
        SelectedSpecies across all rows. ******/
CREATE OR ALTER PROCEDURE [dbo].[spgDiseaseRankingFilterDetail]
	@FilterNumber bigint
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[FilterNumber],
		[DiseaseName],
		[Disease1],
		[Disease2],
		[Disease3],
		[Disease4],
		[Disease5],
		[SelectedSpecies]
	FROM [dbo].[DiseaseFilters]
	WHERE [FilterNumber] = @FilterNumber AND [IsActive] = 1 AND [IsInUse] = 1
END

GO
