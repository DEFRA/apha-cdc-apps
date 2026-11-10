-- Publishing a profile version, and the prioritisation of a draft that the summary prioritisation report runs first, took
-- 1.7 to 3 seconds. Both call sppPrioritisationCalculation (about 0.2 seconds) and then sppPrioritisationScore, which threw
-- away the scores of every profile version (about 21,000 rows built from 165,000 rows of [ProfileVersionSpeciesPrioritisation])
-- and calculated them all again - 1.4 seconds - although only the version that was just prioritised had changed.
-- What changes, and what does not:
--   * sppPrioritisationScore still starts by refreshing the minimum score, maximum score and modifier of every category,
--     exactly as before. It then compares what the scores are calculated from (the ranking range, those category figures,
--     each criterion's category and weighting and each criterion value's score) with a copy kept in the new
--     [PrioritisationScoreBasis] table by the last full calculation. When they differ - the first run after this script, or
--     after the Maintain prioritisation variables page changed a weighting, a score or the range - it calculates every
--     version's scores again, with the original statements, and keeps the new copy. Otherwise it calculates only the
--     versions that have prioritisation data but no scores, which leaves [PrioritisationScore] exactly as the full
--     calculation would.
--   * sppPrioritisationCalculation now deletes the version's scores when the version had prioritisation data (so the next
--     sppPrioritisationScore recalculates that version; a version without any has no scores) and, for each profiled
--     species, reads the version's answers once into an indexed temporary table instead of
--     four times through a common table expression, and finds them through the new ([ProfileVersionId], [SpeciesId]) index
--     of 0080. The rows it writes are unchanged.
--   * spdProfileVersionSpecies deletes the version's scores when it deletes prioritisation data, so a species removed from a
--     prioritised draft is not left in [PrioritisationScore]. Nothing else in it changes.
-- Callers are unchanged: sppPrioritisationScore still takes no parameters, so CDC.Api and the legacy site run the same calls
-- and the same two calls in the same order. Run 0080 first (it supplies the index the faster statements use).
-- All three procedures were created with QUOTED_IDENTIFIER OFF and keep it; [PrioritisationScoreBasis] has no indexes other
-- than its primary key.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'[dbo].[PrioritisationScoreBasis]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[PrioritisationScoreBasis]
		(
		[Kind] char(1) NOT NULL,
		[Id] uniqueidentifier NOT NULL,
		[Ref] uniqueidentifier NULL,
		[Number1] decimal(25,10) NULL,
		[Number2] decimal(25,10) NULL,
		CONSTRAINT [PK_PrioritisationScoreBasis] PRIMARY KEY CLUSTERED ([Kind], [Id])
		)
END

GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE OR ALTER PROCEDURE [dbo].[sppPrioritisationScore]
	
AS
BEGIN
	SET NOCOUNT ON;

	-- read in the upper and lower bounds from the ranking range table
    DECLARE
		@UpperBound as decimal(25,10),
		@LowerBound decimal(25,10),
		@Range decimal(25,10)

	SELECT
		@UpperBound = [UpperBound],
		@LowerBound = [LowerBound],
		@Range = [UpperBound] - [LowerBound]
	FROM
		[PrioritisationRankingRange]
	WHERE
		[PrioritisationType]= 'Profile'

	-- update the PrioritisationCategory table with derived minimum score,
	-- maximum score and modifier figures, based on the values in 
	-- PrioritisationCriterion and PrioritisationCriterionValue
	-- (these may have been changed via the Maintain Prioritisation
	-- Variables page)
	UPDATE
		[PrioritisationCategory]
	SET
		[MinimumScore] = CASE WHEN tmpIrrelevance.[IrrelevanceCount] IS NULL THEN tmpCategory.[MinimumScore] ELSE 0 END,
		[MaximumScore] = tmpCategory.[MaximumScore],
		[Modifier] = (tmpCategory.[MaximumScore] - CASE WHEN tmpIrrelevance.[IrrelevanceCount] IS NULL THEN tmpCategory.[MinimumScore] ELSE 0 END) / @Range
	FROM
		[PrioritisationCategory] INNER JOIN
			(
			SELECT
				[PrioritisationCategory].[Id],	
				SUM(tmpScore.[MinScore] * [PrioritisationCriterion].[Weighting]) AS [MinimumScore],
				SUM(tmpScore.[MaxScore] * [PrioritisationCriterion].[Weighting]) AS [MaximumScore]
			FROM
				[PrioritisationCriterion] INNER JOIN [PrioritisationCategory] ON [PrioritisationCriterion].[PrioritisationCategoryId] = [PrioritisationCategory].[Id]
				INNER JOIN
					(SELECT
						[PrioritisationCriterionId],
						MIN([Score]) AS [MinScore],
						MAX([Score]) AS [MaxScore]
					FROM
						[PrioritisationCriterionValue]
					GROUP BY
						[PrioritisationCriterionId]) tmpScore ON [PrioritisationCriterion].[Id] = tmpScore.[PrioritisationCriterionId]
			GROUP BY
				[PrioritisationCategory].[Id]	
			) tmpCategory ON [PrioritisationCategory].[Id] = tmpCategory.[Id]
		LEFT JOIN
			(
			SELECT
				[PrioritisationCategoryId],
				COUNT([Id]) AS [IrrelevanceCount]
			FROM
				[PrioritisationCategoryIrrelevance]
			GROUP BY
				[PrioritisationCategoryId]
			) AS tmpIrrelevance ON [PrioritisationCategory].[Id] = tmpIrrelevance.[PrioritisationCategoryId]

	-- everything a score is calculated from, apart from the version's prioritisation data
	DECLARE @Basis TABLE
		(
		[Kind] char(1) NOT NULL,
		[Id] uniqueidentifier NOT NULL,
		[Ref] uniqueidentifier NULL,
		[Number1] decimal(25,10) NULL,
		[Number2] decimal(25,10) NULL,
		PRIMARY KEY ([Kind], [Id])
		)

	INSERT INTO @Basis
		(
		[Kind],
		[Id],
		[Ref],
		[Number1],
		[Number2]
		)
	SELECT 'B', CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier), CAST(NULL AS uniqueidentifier), CAST(@LowerBound AS decimal(25,10)), CAST(NULL AS decimal(25,10))
	UNION ALL
	SELECT 'C', [Id], CAST(NULL AS uniqueidentifier), CAST([MinimumScore] AS decimal(25,10)), CAST([Modifier] AS decimal(25,10)) FROM [PrioritisationCategory]
	UNION ALL
	SELECT 'K', [Id], [PrioritisationCategoryId], CAST([Weighting] AS decimal(25,10)), CAST(NULL AS decimal(25,10)) FROM [PrioritisationCriterion]
	UNION ALL
	SELECT 'V', [Id], CAST(NULL AS uniqueidentifier), CAST([Score] AS decimal(25,10)), CAST(NULL AS decimal(25,10)) FROM [PrioritisationCriterionValue]

	-- did anything the scores are calculated from change since they were all last calculated?
	DECLARE @BasisChanges int

	SELECT
		@BasisChanges = COUNT(*)
	FROM
		@Basis current_basis FULL OUTER JOIN [PrioritisationScoreBasis] kept_basis ON current_basis.[Kind] = kept_basis.[Kind] AND current_basis.[Id] = kept_basis.[Id]
	WHERE
		current_basis.[Kind] IS NULL OR
		kept_basis.[Kind] IS NULL OR
		NOT EXISTS (
			SELECT current_basis.[Ref], current_basis.[Number1], current_basis.[Number2]
			INTERSECT
			SELECT kept_basis.[Ref], kept_basis.[Number1], kept_basis.[Number2]
		)

	IF @BasisChanges > 0 BEGIN

		-- rebuild the data in the PrioritisationScore table
		DELETE FROM [PrioritisationScore]

		-- insert the per-category data for categories that have values in
		-- [ProfileVersionSpeciesPrioritisation]
		INSERT INTO [PrioritisationScore]
			(
			[ProfileVersionId],
			[SpeciesId],
			[PrioritisationCategoryId],
			[Score],
			[Rank]
			)
		SELECT
			[ProfileVersionId],
			[SpeciesId],
			[PrioritisationCategoryId],
			CASE WHEN [DataIncomplete] = 1 THEN NULL ELSE [Score] END AS [Score],
			CASE WHEN [DataIncomplete] = 1 THEN NULL ELSE ROUND(@LowerBound + (CAST([Score] AS decimal(25,10)) - CAST([MinimumScore] AS decimal(25,10))) / [Modifier], 0) END AS [Rank]
		FROM
			(
			SELECT
				[ProfileVersionSpeciesPrioritisation].[ProfileVersionId],
				[ProfileVersionSpeciesPrioritisation].[SpeciesId],
				[PrioritisationCriterion].[PrioritisationCategoryId],
				ISNULL(SUM([PrioritisationCriterionValue].[Score] * [PrioritisationCriterion].[Weighting]), 0) AS [Score],
				MAX(CAST([ProfileVersionSpeciesPrioritisation].[IsMissing] AS integer)) AS [DataIncomplete]
			FROM
				[ProfileVersionSpeciesPrioritisation] INNER JOIN [PrioritisationCriterion] ON [ProfileVersionSpeciesPrioritisation].[PrioritisationCriterionId] = [PrioritisationCriterion].[Id]
				LEFT JOIN [PrioritisationCriterionValue] ON [ProfileVersionSpeciesPrioritisation].[PrioritisationCriterionValueId] = [PrioritisationCriterionValue].[Id]
			GROUP BY
				[ProfileVersionSpeciesPrioritisation].[ProfileVersionId],
				[ProfileVersionSpeciesPrioritisation].[SpeciesId],
				[PrioritisationCriterion].[PrioritisationCategoryId]
			) tmpTotal
			INNER JOIN [PrioritisationCategory] ON tmpTotal.[PrioritisationCategoryId] = [PrioritisationCategory].[Id]

		-- remember what the scores were calculated from
		DELETE FROM [PrioritisationScoreBasis]

		INSERT INTO [PrioritisationScoreBasis]
			(
			[Kind],
			[Id],
			[Ref],
			[Number1],
			[Number2]
			)
		SELECT
			[Kind],
			[Id],
			[Ref],
			[Number1],
			[Number2]
		FROM
			@Basis

	END ELSE BEGIN

		-- the versions that have prioritisation data but no scores: their data was rebuilt by sppPrioritisationCalculation
		-- (which deletes the version's scores) or its species were deleted by spdProfileVersionSpecies since the last run
		DECLARE @Refresh TABLE
			(
			[ProfileVersionId] uniqueidentifier NOT NULL PRIMARY KEY
			)

		INSERT INTO @Refresh
			(
			[ProfileVersionId]
			)
		SELECT DISTINCT
			[ProfileVersionPrioritisedSpecies].[ProfileVersionId]
		FROM
			[ProfileVersionPrioritisedSpecies]
		WHERE
			NOT EXISTS (
				SELECT
					1
				FROM
					[PrioritisationScore]
				WHERE
					[PrioritisationScore].[ProfileVersionId] = [ProfileVersionPrioritisedSpecies].[ProfileVersionId]
			)
		OPTION (HASH JOIN)

		-- the same statement as the full calculation above, for those versions only
		INSERT INTO [PrioritisationScore]
			(
			[ProfileVersionId],
			[SpeciesId],
			[PrioritisationCategoryId],
			[Score],
			[Rank]
			)
		SELECT
			[ProfileVersionId],
			[SpeciesId],
			[PrioritisationCategoryId],
			CASE WHEN [DataIncomplete] = 1 THEN NULL ELSE [Score] END AS [Score],
			CASE WHEN [DataIncomplete] = 1 THEN NULL ELSE ROUND(@LowerBound + (CAST([Score] AS decimal(25,10)) - CAST([MinimumScore] AS decimal(25,10))) / [Modifier], 0) END AS [Rank]
		FROM
			(
			SELECT
				[ProfileVersionSpeciesPrioritisation].[ProfileVersionId],
				[ProfileVersionSpeciesPrioritisation].[SpeciesId],
				[PrioritisationCriterion].[PrioritisationCategoryId],
				ISNULL(SUM([PrioritisationCriterionValue].[Score] * [PrioritisationCriterion].[Weighting]), 0) AS [Score],
				MAX(CAST([ProfileVersionSpeciesPrioritisation].[IsMissing] AS integer)) AS [DataIncomplete]
			FROM
				@Refresh refresh INNER JOIN [ProfileVersionSpeciesPrioritisation] ON refresh.[ProfileVersionId] = [ProfileVersionSpeciesPrioritisation].[ProfileVersionId]
				INNER JOIN [PrioritisationCriterion] ON [ProfileVersionSpeciesPrioritisation].[PrioritisationCriterionId] = [PrioritisationCriterion].[Id]
				LEFT JOIN [PrioritisationCriterionValue] ON [ProfileVersionSpeciesPrioritisation].[PrioritisationCriterionValueId] = [PrioritisationCriterionValue].[Id]
			GROUP BY
				[ProfileVersionSpeciesPrioritisation].[ProfileVersionId],
				[ProfileVersionSpeciesPrioritisation].[SpeciesId],
				[PrioritisationCriterion].[PrioritisationCategoryId]
			) tmpTotal
			INNER JOIN [PrioritisationCategory] ON tmpTotal.[PrioritisationCategoryId] = [PrioritisationCategory].[Id]

	END

END

GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE OR ALTER PROCEDURE [dbo].[sppPrioritisationCalculation]
	@ProfileVersionId uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

    DECLARE @SpeciesId uniqueidentifier
	DECLARE @HadPrioritisationData bit = 0

	IF EXISTS (
		SELECT
			1
		FROM
			[ProfileVersionPrioritisedSpecies]
		WHERE
			[ProfileVersionId] = @ProfileVersionId
	) BEGIN
		SET @HadPrioritisationData = 1
	END

	DELETE
		[ProfileVersionSpeciesPrioritisation]
	WHERE
		[ProfileVersionId] = @ProfileVersionId

	DELETE
		[ProfileVersionPrioritisedSpecies]
	WHERE
		[ProfileVersionId] = @ProfileVersionId

	-- the version's scores are out of date as soon as its prioritisation data is rebuilt below;
	-- sppPrioritisationScore calculates the scores of every version that has none. A version that
	-- had no prioritisation data has no scores either.
	IF @HadPrioritisationData = 1 BEGIN

		DELETE FROM
			[PrioritisationScore]
		WHERE
			[ProfileVersionId] = @ProfileVersionId

	END
	  
	DECLARE @SpeciesList TABLE
		(
		SpeciesId uniqueidentifier
		)

	-- the version's answers that apply to the profiled species being worked on: those not recorded against a species, and
	-- those recorded against that species. [ListValue] is the lookup text of the selected value, [ListValueId] its identifier.
	SELECT TOP 0
		[ProfileVersionFieldValue].[ProfileFieldId],
		[ProfileVersionFieldValue].[BooleanValue],
		[ProfileVersionFieldValue].[ListValue] AS [ListValueId],
		[ReferenceValue].[LookupValue] AS [ListValue]
	INTO
		#ProfileSpeciesData
	FROM
		[ProfileVersionFieldValue] LEFT JOIN [ReferenceValue] ON [ProfileVersionFieldValue].[ListValue] = [ReferenceValue].[Id]

	CREATE CLUSTERED INDEX [IX_ProfileSpeciesData] ON #ProfileSpeciesData ([ProfileFieldId])

	DECLARE csrProfileSpecies CURSOR FOR
		SELECT
			[SpeciesId]
		FROM
			[ProfileVersionSpecies] INNER JOIN [luAffectedSpeciesType] ON [ProfileVersionSpecies].[AffectedSpeciesTypeId] = [luAffectedSpeciesType].[Id]
		WHERE
			[ProfileVersionId] = @ProfileVersionId AND
			[luAffectedSpeciesType].[Name] = 'Profiled'
	FOR READ ONLY

	OPEN csrProfileSpecies

	FETCH NEXT FROM csrProfileSpecies INTO @SpeciesId

	WHILE @@FETCH_STATUS = 0 BEGIN

		INSERT INTO @SpeciesList
		SELECT
			[Id]
		FROM
			dbo.GetChildSpecies(@SpeciesId)

		INSERT INTO [ProfileVersionPrioritisedSpecies]
		SELECT
			@ProfileVersionId,
			SpeciesId,
			@SpeciesId
		FROM
			@SpeciesList
		;

		DELETE FROM #ProfileSpeciesData

		INSERT INTO #ProfileSpeciesData
			(
			[ProfileFieldId],
			[BooleanValue],
			[ListValueId],
			[ListValue]
			)
		SELECT
			[ProfileFieldId],
			[BooleanValue],
			[ProfileVersionFieldValue].[ListValue],
			[ReferenceValue].[LookupValue]
		FROM
			[ProfileVersionFieldValue] LEFT JOIN [ReferenceValue] ON [ProfileVersionFieldValue].[ListValue] = [ReferenceValue].[Id]
		WHERE
			[ProfileVersionId] = @ProfileVersionId AND
			([SpeciesId] IS NULL OR [SpeciesId] = @SpeciesId)

		;WITH IrrelevanceData (PrioritisationCategoryId, IsIrrelevant, IsMissing)
		AS
		(
		SELECT
			[PrioritisationCategoryIrrelevance].[PrioritisationCategoryId],
			MAX(
			CASE WHEN [PrioritisationCategoryIrrelevance].[IrrelevanceValueId] IS NOT NULL THEN 
				CASE WHEN [PrioritisationCategoryIrrelevance].[IrrelevanceValueId] = speciesData.[ListValueId] THEN 1 ELSE 0 END
			ELSE
				CASE WHEN [PrioritisationCategoryIrrelevance].[IrrelevanceFlag] = speciesData.[BooleanValue] THEN 1 ELSE 0 END
			END
			)
			AS [IsIrrelevant],
			MAX(
			CASE WHEN [PrioritisationCategoryIrrelevance].[IrrelevanceValueId] IS NOT NULL THEN 
				CASE WHEN speciesData.[ListValueId] IS NULL THEN 1 ELSE 0 END
			ELSE
				CASE WHEN speciesData.[BooleanValue] IS NULL THEN 1 ELSE 0 END
			END
			)
			AS [IsMissing]
		FROM
			[PrioritisationCategoryIrrelevance] LEFT JOIN #ProfileSpeciesData speciesData ON 
				[PrioritisationCategoryIrrelevance].[IrrelevanceFieldId] = speciesData.[ProfileFieldId]
		GROUP BY
			[PrioritisationCategoryIrrelevance].[PrioritisationCategoryId]
		)
		INSERT INTO [ProfileVersionSpeciesPrioritisation]
			(
			[ProfileVersionId],
			[SpeciesId],
			[PrioritisationCriterionId],
			[PrioritisationCriterionValueId],
			[IsMissing]
			)
		SELECT
			@ProfileVersionId,
			speciesList.[SpeciesId],
			[PrioritisationCriterion].[Id] AS PrioritisationCriterionId,
			CASE WHEN ISNULL(IrrelevanceData.IsIrrelevant, 0) = 1 THEN NULL WHEN ISNULL(MappingData.BooleanValue, 1) = 1 THEN TrueValue.Id ELSE FalseValue.Id END AS PrioritisationCriterionValueId,
			CASE WHEN (CASE WHEN ISNULL(MappingData.BooleanValue, 1) = 1 THEN TrueValue.Id ELSE FalseValue.Id END IS NULL OR ISNULL(IrrelevanceData.IsMissing, 0) = 1) AND ISNULL(IrrelevanceData.IsIrrelevant, 0) = 0 THEN 1 ELSE 0 END AS IsMissing
		FROM
			[PrioritisationCriterion] LEFT JOIN IrrelevanceData ON [PrioritisationCriterion].[PrioritisationCategoryId] = IrrelevanceData.PrioritisationCategoryId
			LEFT JOIN #ProfileSpeciesData MappingData ON [PrioritisationCriterion].[MappingFlag] = MappingData.ProfileFieldId
			LEFT JOIN #ProfileSpeciesData TrueData ON [PrioritisationCriterion].[TrueMapping] = TrueData.ProfileFieldId
			LEFT JOIN #ProfileSpeciesData FalseData ON [PrioritisationCriterion].[FalseMapping] = FalseData.ProfileFieldId
			LEFT JOIN [PrioritisationCriterionValue] TrueValue ON ([PrioritisationCriterion].[Id] = TrueValue.[PrioritisationCriterionId] AND TrueData.ListValue = LEFT(TrueValue.CriterionValue, LEN(TrueData.ListValue)))
			LEFT JOIN [PrioritisationCriterionValue] FalseValue ON ([PrioritisationCriterion].[Id] = FalseValue.[PrioritisationCriterionId] AND FalseData.ListValue = LEFT(FalseValue.CriterionValue, LEN(FalseData.ListValue)))
			, @SpeciesList speciesList


		DELETE FROM @SpeciesList
    
		FETCH NEXT FROM csrProfileSpecies INTO @SpeciesId

	END

    CLOSE csrProfileSpecies
    DEALLOCATE csrProfileSpecies

END

GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE OR ALTER PROCEDURE [dbo].[spdProfileVersionSpecies]
	@ProfileVersionId uniqueidentifier,
	@SpeciesId uniqueidentifier
AS
BEGIN

		SET NOCOUNT ON;

		-- delete per-species field values
		DELETE
			[ProfileVersionFieldValue]
		WHERE
			[ProfileVersionId] = @ProfileVersionId AND
			[SpeciesId] = @SpeciesId	
 
		--delete from prioritisation tables
		DELETE
			[ProfileVersionSpeciesPrioritisation]
		FROM
			[ProfileVersionSpeciesPrioritisation] INNER JOIN [ProfileVersionPrioritisedSpecies] ON [ProfileVersionSpeciesPrioritisation].[ProfileVersionId] = [ProfileVersionPrioritisedSpecies].[ProfileVersionId] AND [ProfileVersionSpeciesPrioritisation].[SpeciesId] = [ProfileVersionPrioritisedSpecies].[SpeciesId]
		WHERE
			[ProfileVersionSpeciesPrioritisation].[ProfileVersionId] = @ProfileVersionId AND
			[ProfileVersionPrioritisedSpecies].[AffectedSpeciesId] = @SpeciesId

		-- the version's scores no longer match its prioritisation data; sppPrioritisationScore calculates them again
		IF @@ROWCOUNT > 0 BEGIN

			DELETE FROM
				[PrioritisationScore]
			WHERE
				[ProfileVersionId] = @ProfileVersionId

		END

		DELETE
			[ProfileVersionPrioritisedSpecies]
		WHERE
			[ProfileVersionPrioritisedSpecies].[ProfileVersionId] = @ProfileVersionId AND
			[ProfileVersionPrioritisedSpecies].[AffectedSpeciesId] = @SpeciesId
	  
		--delete the row in the link table to indicate that this species is affected for this profile version
		DELETE 
			[ProfileVersionSpecies]
		WHERE
			[ProfileVersionId] = @ProfileVersionId AND
			[SpeciesId] = @SpeciesId

 END

GO
