-- spgReferenceValueByTable worked out each value's [IsInUse] by grouping every row of [ProfileVersionFieldValue]
-- (about 800,000 rows) on every call, which took about 0.7 seconds per call. CDC.Api calls it once for each maintainable
-- reference list a profile version answers, so validating a profile version (every section tab, Validate, Publish) and
-- building a report took 7 to 10 seconds. An index on [ListValue] lets the procedure look up whether one value is used
-- instead, and the procedure now does that. The columns, the rows and the order returned are unchanged.
-- The index is deliberately not a filtered index (WHERE [ListValue] IS NOT NULL): several legacy procedures that write
-- [ProfileVersionFieldValue] (for example spuProfileVersionFieldValue) were created with QUOTED_IDENTIFIER OFF, and SQL Server
-- rejects their writes with error 1934 once the table has a filtered index.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionFieldValue_ListValue'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionFieldValue]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ProfileVersionFieldValue_ListValue]
		ON [dbo].[ProfileVersionFieldValue] ([ListValue])
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spgReferenceValueByTable]
	@ReferenceTableId uniqueidentifier
AS
BEGIN

	SET NOCOUNT ON;

	IF NOT EXISTS(
		SELECT
			[Id]
		FROM
			[ReferenceTable]
		WHERE
			[Id] = @ReferenceTableId AND
			[IsMaintainable] = 1
	) BEGIN
		RAISERROR('The specified reference table does not exist or is not maintainable', 16, 1)
		RETURN
	END

	SELECT
		[ReferenceValue].[Id],
		[ReferenceValue].[LookupValue],
		[ReferenceValue].[EffectiveDateFrom],
		[ReferenceValue].[EffectiveDateTo],
		CAST(CASE WHEN EXISTS (
			SELECT 1
			FROM [ProfileVersionFieldValue]
			WHERE [ProfileVersionFieldValue].[ListValue] = [ReferenceValue].[Id]
		) THEN 1 ELSE 0 END AS bit) AS [IsInUse],
		CAST(CASE WHEN [ReferenceValue].[EffectiveDateTo] IS NULL THEN 1 ELSE 0 END AS bit) AS [IsActive]
	FROM
		[ReferenceValue]
	WHERE
		[ReferenceValue].[ReferenceTableId] = @ReferenceTableId
	ORDER BY
		ISNULL([SequenceNumber], 255),
		[EffectiveDateFrom]

END

GO
