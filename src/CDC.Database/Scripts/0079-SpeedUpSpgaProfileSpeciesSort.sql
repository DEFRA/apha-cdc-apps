-- spgaProfile sorted its fourth result set (the species of every profile version, about 17,000 rows) by
-- dbo.udf_StripHTMLTags([Species].[Name]), so the scalar function ran once for every row. That took about 4.5 of the
-- 4.8 seconds the procedure needs, and every Search, Compare versions and disease-ranking load called it. The species are
-- now ranked by that same key once (about 130 rows) and the rows are sorted by the rank, so the columns, the rows and the
-- order returned are unchanged. Every other statement is as it was in 0050-UpdateSearchProfileStoredProcedure.sql of the
-- legacy database, except that the 'Draft' and 'Complete' literals are held in two variables and the two dynamic
-- statements are written without line breaks inside their string literals (code-analysis rules); neither changes what
-- is returned.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spgaProfile]
	@UserId uniqueidentifier,
	@ReturnQuestionAnswers BIT = 0,
	@ReturnNotes BIT = 0,
	@Words NVARCHAR(2000),
	@PerformSqlSearch BIT,
	@SectionSelection VARCHAR(MAX)
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @DateNow smalldatetime
	DECLARE @SearchSql NVARCHAR(MAX);
	DECLARE @DraftState varchar(50) = 'Draft'
	DECLARE @CompleteStatus varchar(50) = 'Complete'

	SET @DateNow = DATEADD(mi, 1, GETDATE()) 

      SELECT
		[Profile].[Id],
		[Profile].[Title]
	FROM
		[Profile]
	WHERE
		[Profile].[ParentId] IS NULL
	ORDER BY
		dbo.udf_StripHTMLTags([Profile].[Title])

	SELECT
		[Profile].[Id] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		ISNULL([Profile].[ScenarioTitle], 'Current situation') AS [ScenarioTitle],		
		[luProfileUserRole].[Name] AS [UserRole],
		[luProfileStatusType].[Name] AS [ProfileStatus]
	FROM
		[Profile] LEFT JOIN [ProfileUser] ON ([Profile].[Id] = [ProfileUser].[ProfileId] AND [ProfileUser].[UserId] = @UserId)
		LEFT JOIN [luProfileUserRole] ON [ProfileUser].[ProfileUserRoleId] = [luProfileUserRole].[Id] 
		LEFT JOIN [luProfileStatusType] ON [Profile].[ProfileStatusId] = [luProfileStatusType].[Id]
	ORDER BY
		dbo.udf_StripHTMLTags([Profile].[ScenarioTitle])

	
;WITH contributionDatesCTE AS (SELECT MAX(LastContributionDate) as LastContributionDate, ProfileVersionId
FROM [ProfileVersionSectionUser]
GROUP BY ProfileVersionId)
SELECT
		[ProfileVersion].[Id],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[ProfileVersion].[VersionMajor],
		[ProfileVersion].[VersionMinor],
		[luProfileVersionState].[Name],
		[ProfileVersion].[EffectiveDateFrom],
		[ProfileVersion].[EffectiveDateTo],
		[ProfileVersion].[IsPublic],
		[ContributionDate].LastContributionDate
	FROM
		[ProfileVersion] INNER JOIN [luProfileVersionState] ON [ProfileVersion].[ProfileVersionStateId] = [luProfileVersionState].[Id]
		INNER JOIN [Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id]
		LEFT JOIN contributionDatesCTE ContributionDate on ContributionDate.ProfileVersionId = [ProfileVersion].Id
	ORDER BY
		[ProfileVersion].[VersionMajor] DESC,
		[ProfileVersion].[VersionMinor] DESC

	SELECT
		[ProfileVersion].[Id],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[Species].[Name],
		[Species].[Id],
		[luAffectedSpeciesType].[Name] AS [AffectedSpeciesType],
		CAST(CASE WHEN [Species].[EffectiveDateTo] IS NULL THEN 1 WHEN @DateNow BETWEEN [Species].[EffectiveDateFrom] AND [Species].[EffectiveDateTo] THEN 1 ELSE 0 END AS bit) AS [IsActive]
	FROM
		[ProfileVersion] INNER JOIN [ProfileVersionSpecies] ON [ProfileVersion].[Id] = [ProfileVersionSpecies].[ProfileVersionId]
		INNER JOIN [Species] ON [ProfileVersionSpecies].[SpeciesId] = [Species].[Id]
		INNER JOIN [Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id]
		INNER JOIN [luAffectedSpeciesType] ON [ProfileVersionSpecies].[AffectedSpeciesTypeId] = [luAffectedSpeciesType].[Id]
		INNER JOIN (
			SELECT
				[Species].[Id] AS [SpeciesId],
				DENSE_RANK() OVER (ORDER BY dbo.udf_StripHTMLTags([Species].[Name])) AS [SortRank]
			FROM
				[Species]
		) [SpeciesSortRank] ON [SpeciesSortRank].[SpeciesId] = [Species].[Id]
	ORDER BY
		[SpeciesSortRank].[SortRank]
	
	-- Technical Review	
	SELECT 
		[ProfileVersionId],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[ProfileVersion].[Title],
		MIN([NextTechnicalReview])		
	FROM 
		[ProfileVersionSection] INNER JOIN 	
		[ProfileVersion] ON [ProfileVersionSection].[ProfileVersionId] = [ProfileVersion].[Id] INNER JOIN
		[luProfileVersionState] ON [luProfileVersionState].[Id] = [ProfileVersion].[ProfileVersionStateId] INNER JOIN
		[Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id] LEFT JOIN
		[luReviewStatus] tS ON tS.[Id] = [ProfileVersionSection].[TechnicalReviewStatusId]		
	WHERE
		[ProfileVersion].[EffectiveDateTo] IS NULL AND
		[luProfileVersionState].[Name] = @DraftState AND	
		((tS.Name <> @CompleteStatus)
		OR (tS.Name IS NULL))	
	GROUP BY
		[ProfileVersionId], 
		[ProfileVersion].[ProfileId], 
		ISNULL([Profile].[ParentId], [Profile].[Id]),
		[ProfileVersion].[Title]
	HAVING
		MIN([NextTechnicalReview]) IS NOT NULL
		
	-- Policy Review
	SELECT 
		[ProfileVersionId],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[ProfileVersion].[Title],
		MIN([NextPolicyReview])		
	FROM 
		[ProfileVersionSection] INNER JOIN 	
		[ProfileVersion] ON [ProfileVersionSection].[ProfileVersionId] = [ProfileVersion].[Id] INNER JOIN
		[luProfileVersionState] ON [luProfileVersionState].[Id] = [ProfileVersion].[ProfileVersionStateId] INNER JOIN
		[Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id] LEFT JOIN
		[luReviewStatus] policyStatus ON policyStatus.[Id] = [ProfileVersionSection].[PolicyReviewStatusId]						
	WHERE
		[ProfileVersion].[EffectiveDateTo] IS NULL AND
		[luProfileVersionState].[Name] = @DraftState AND
		((policyStatus.Name <> @CompleteStatus) OR
		(policyStatus.Name IS NULL))
	GROUP BY
		[ProfileVersionId], 
		[ProfileVersion].[ProfileId], 
		ISNULL([Profile].[ParentId], [Profile].[Id]),
		[ProfileVersion].[Title]
	HAVING
		MIN([NextPolicyReview]) IS NOT NULL
	
	SELECT 
		[ProfileVersionId],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[ProfileVersion].[Title]
	FROM 
		[ProfileVersionSection] INNER JOIN 	
		[ProfileVersion] ON [ProfileVersionSection].[ProfileVersionId] = [ProfileVersion].[Id] INNER JOIN
		[luProfileVersionState] ON [luProfileVersionState].[Id] = [ProfileVersion].[ProfileVersionStateId] INNER JOIN
		[Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id] LEFT JOIN
		[luReviewStatus] techStatus ON techStatus.[Id] = [ProfileVersionSection].[TechnicalReviewStatusId]		
	WHERE
		[ProfileVersion].[EffectiveDateTo] IS NULL AND
		[luProfileVersionState].[Name] = @DraftState AND
		((
		techStatus.Name <> @CompleteStatus		
		)
		OR
		(
		techStatus.Name IS NULL
		))
		AND
		(
		[ProfileVersionSection].[NextTechnicalReview] is null AND
		[ProfileVersionSection].[NextPolicyReview] is null
		)	
	
	SELECT 
		[ProfileVersionId],
		[ProfileVersion].[ProfileId] AS [ScenarioId],
		ISNULL([Profile].[ParentId], [Profile].[Id]) AS [ProfileId],
		[ProfileVersion].[Title]
	FROM 
		[ProfileVersionSection] INNER JOIN 	
		[ProfileVersion] ON [ProfileVersionSection].[ProfileVersionId] = [ProfileVersion].[Id] INNER JOIN
		[luProfileVersionState] ON [luProfileVersionState].[Id] = [ProfileVersion].[ProfileVersionStateId] INNER JOIN
		[Profile] ON [ProfileVersion].[ProfileId] = [Profile].[Id] LEFT JOIN
		[luReviewStatus] policyStatus ON policyStatus.[Id] = [ProfileVersionSection].[PolicyReviewStatusId]		
	WHERE
		[ProfileVersion].[EffectiveDateTo] IS NULL AND
		[luProfileVersionState].[Name] = @DraftState AND
		((
		policyStatus.Name <> @CompleteStatus		
		)
		OR
		(
		policyStatus.Name IS NULL
		))
		AND
		(
		[ProfileVersionSection].[NextPolicyReview] is null AND
		[ProfileVersionSection].[NextTechnicalReview] is null
		)		
	
	IF @ReturnQuestionAnswers = 1 BEGIN	
		SET @SearchSql =
			'SELECT pvfv.Id AS AnswerId, pv.Id AS ProfileVersionId, pv.ProfileId AS ScenarioId, ISNULL(p.ParentId, p.Id) AS ProfileId, ' +
			'ps.SectionNumber, pq.QuestionNumber, pvfv.TextValue, pq.ShortName, pq.UserGuidance ' +
			'FROM ProfileVersion pv ' +
			'INNER JOIN luProfileVersionState pvs WITH (NOLOCK) ON pv.ProfileVersionStateId = pvs.Id ' +
			'INNER JOIN [Profile] p WITH (NOLOCK) ON pv.ProfileId = p.Id ' +
			'INNER JOIN ProfileVersionFieldValue pvfv WITH (NOLOCK) ON pvfv.ProfileVersionId = pv.Id ' +
			'INNER JOIN ProfileField pf WITH (NOLOCK) ON pf.Id = pvfv.ProfileFieldId ' +
			'INNER JOIN ProfileQuestion pq WITH (NOLOCK) ON pq.Id = pf.ProfileQuestionId ' +
			'INNER JOIN ProfileSection ps WITH (NOLOCK) ON ps.Id = pq.ProfileSectionId ' +
			'INNER JOIN luDataFieldType df WITH (NOLOCK) ON df.Id = pf.DataFieldTypeId ' +
			'WHERE pv.EffectiveDateTo IS NULL AND ' +
			'(df.[Name] = ''Text'' OR df.[Name] = ''Long Text'') ';

		IF @PerformSqlSearch = 1 BEGIN
			DECLARE @Phrases TABLE(Word NVARCHAR(255))
			INSERT INTO @Phrases SELECT * FROM dbo.fn_splitWords(@Words)

			SET @SearchSql = @SearchSql + 'AND (' + STUFF((SELECT ' OR pvfv.TextValue LIKE ''%' + Word + '%''' FROM @Phrases FOR XML PATH('')), 1, 4, '') + ' OR ' + STUFF((SELECT ' OR pq.UserGuidance LIKE ''%' + Word + '%''' FROM @Phrases FOR XML PATH('')), 1, 4, '') + ') ';
		END

		IF  (ISNULL(@SectionSelection, '') <> '')
		BEGIN
			SET @SearchSql = @SearchSql + ' AND ps.SectionNumber IN (' + ISNULL(@SectionSelection, 'NULL') + ')';
		END

		EXEC sp_executesql @SearchSql

	END
	ELSE
	BEGIN
		SELECT TOP 0 
            NULL AS AnswerId,
			NULL AS ProfileVersionId,
            NULL AS ScenarioId,
            NULL AS ProfileId,
            NULL AS SectionNumber,
			NULL AS QuestionNumber,
            NULL AS TextValue,
			NULL AS ShortName,
			NULL AS UserGuidance

	END
	
	IF @ReturnNotes = 1 BEGIN	
		SET @SearchSql =
			'SELECT pvn.Id AS NoteId, pv.Id AS ProfileVersionId, pv.ProfileId AS ScenarioId, ISNULL(p.ParentId, p.Id) AS ProfileId, ' +
			'pvn.NoteText, IsReference, lpnt.[Name] ' +
			'FROM ProfileVersion pv ' +
			'INNER JOIN ProfileVersionNote pvn WITH (NOLOCK) ON pv.Id = pvn.ProfileVersionId ' +
			'INNER JOIN luProfileNoteType lpnt WITH (NOLOCK) ON lpnt.Id = pvn.ProfileNoteTypeId ' +
			'INNER JOIN [Profile] p WITH (NOLOCK) ON pv.ProfileId = p.Id ' +
			'WHERE pv.EffectiveDateTo IS NULL '

		IF @PerformSqlSearch = 1 BEGIN
			SET @SearchSql = @SearchSql + ' AND ' + STUFF((SELECT ' AND pvn.NoteText LIKE ''%' + Word + '%''' FROM @Phrases FOR XML PATH('')), 1, 4, '') + ' ';
		END
		
		EXEC sp_executesql @SearchSql;

	END
	ELSE
	BEGIN
		SELECT TOP 0 
            NULL AS NoteId,
			NULL AS ProfileVersionId,
            NULL AS ScenarioId,
            NULL AS ProfileId,
            NULL AS NoteText,
            NULL AS IsReference,
			NULL AS [Name];
	END

	

END

GO
