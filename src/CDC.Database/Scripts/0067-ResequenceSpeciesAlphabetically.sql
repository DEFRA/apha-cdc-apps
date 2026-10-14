/****** One-off data fix: resequences every species' SequenceNumber to alphabetical order
        within its parent group (root groups included), closing any gaps at the same time.
        Needed because the real stored order (used throughout the application, including the
        "Maintain species data" hierarchy and Reorder list moves) had drifted a long way from
        alphabetical over the data's history - for example root groups read Mammals, Avians,
        Reptiles, Amphibians, Fish, Molluscs, Crustacea by SequenceNumber. Requested as a
        temporary reset back to alphabetical; a future change can re-sequence deliberately. ******/
;WITH Ordered AS (
	SELECT
		[Id],
		ROW_NUMBER() OVER (
			PARTITION BY ISNULL([ParentId], '00000000-0000-0000-0000-000000000000')
			ORDER BY [Name]
		) AS [NewSequenceNumber]
	FROM
		[Species]
)
UPDATE [s]
SET
	[s].[SequenceNumber] = [o].[NewSequenceNumber]
FROM
	[Species] [s]
	INNER JOIN Ordered [o] ON [o].[Id] = [s].[Id]
WHERE
	[s].[SequenceNumber] IS NULL OR [s].[SequenceNumber] <> [o].[NewSequenceNumber]

GO
