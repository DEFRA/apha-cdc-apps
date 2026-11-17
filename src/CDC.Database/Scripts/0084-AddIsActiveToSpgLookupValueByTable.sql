-- spgLookupValueByTable returned every value of a reference table, including values that have been inactivated, so the
-- profile questions page showed an inactive value as a selectable option (legacy CheckBoxListProfileField hid an inactive
-- value unless it was already selected). This adds an [IsActive] column, after the existing ones, so the page can do the same.
-- The existing columns, the rows and the order returned are unchanged.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spgLookupValueByTable]
	@ReferenceTableId uniqueidentifier
AS
BEGIN

	SET NOCOUNT ON;

	SELECT
		[ReferenceValue].[Id],
		[ReferenceValue].[LookupValue],
		CAST(CASE WHEN [ReferenceValue].[EffectiveDateTo] IS NULL THEN 1 ELSE 0 END AS bit) AS [IsActive]
	FROM
		[ReferenceValue]
	WHERE
		[ReferenceValue].[ReferenceTableId] = @ReferenceTableId
	ORDER BY
		[SequenceNumber]

END

GO
