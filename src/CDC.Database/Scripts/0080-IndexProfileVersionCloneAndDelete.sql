-- Creating, deleting and publishing a profile version took 3 to 9 seconds (clone a profile or scenario, create a new draft
-- version, delete a draft version) because the profile tables are joined and checked on columns that have no index:
--   * [ProfileVersionFieldValue].[ProfileVersionQuestionRowId] (about 800,000 rows): every row deleted from
--     [ProfileVersionQuestionRow] made SQL Server read the whole of [ProfileVersionFieldValue] to prove no value still
--     points at it (spdProfileVersion spent 2 seconds there), and spiProfileVersionSpecies read it all again for every
--     profiled species it copied.
--   * [ProfileVersionFieldValue] had an index on [ProfileVersionId] and another on [SpeciesId] but none on both, so the
--     per-species copy, trade-data refresh and delete statements read every value of the version (or every value of the
--     species in every version) to find the 100 or so rows they want. One index on ([ProfileVersionId], [SpeciesId])
--     serves them and also every lookup by [ProfileVersionId] alone, so it replaces the single-column index (one index
--     fewer to maintain for each of the 800 values a new version copies).
--   * [ProfileVersionNote].[ProfileVersionId] and [ReviewComment].[ProfileVersionId] / [ParentId] had no index, so
--     copying or deleting a version's notes and comments read both whole tables several times; the review comment copy in
--     spiProfileVersion is a recursive query that did so once per comment (0.9 seconds).
-- The indexes are not filtered indexes (WHERE ... IS NOT NULL): several legacy procedures that write
-- [ProfileVersionFieldValue] (for example spuProfileVersionFieldValue) were created with QUOTED_IDENTIFIER OFF, and SQL Server
-- rejects their writes with error 1934 once the table has a filtered index. No data or procedure changes, so the legacy site
-- is unaffected; the script takes a few seconds on the 800,000 rows.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionFieldValue_ProfileVersionQuestionRowId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionFieldValue]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ProfileVersionFieldValue_ProfileVersionQuestionRowId]
		ON [dbo].[ProfileVersionFieldValue] ([ProfileVersionQuestionRowId])
END

GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionFieldValue_ProfileVersionId_SpeciesId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionFieldValue]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ProfileVersionFieldValue_ProfileVersionId_SpeciesId]
		ON [dbo].[ProfileVersionFieldValue] ([ProfileVersionId], [SpeciesId])
END

GO

-- Superseded by the index above (same leading column); no procedure names it in a hint.
IF EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionFieldValue_ProfileVersionId_SpeciesId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionFieldValue]'))
	AND EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionFieldValue_ProfileVersionId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionFieldValue]'))
BEGIN
	DROP INDEX [IX_ProfileVersionFieldValue_ProfileVersionId] ON [dbo].[ProfileVersionFieldValue]
END

GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ProfileVersionNote_ProfileVersionId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ProfileVersionNote]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ProfileVersionNote_ProfileVersionId]
		ON [dbo].[ProfileVersionNote] ([ProfileVersionId])
END

GO

-- ([ProfileVersionId], [ProfileSectionId]) are the columns of the foreign key to [ProfileVersionSection], which SQL Server
-- checks every time a section row is deleted; it also serves every lookup of a version's comments.
IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ReviewComment_ProfileVersionId_ProfileSectionId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ReviewComment]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ReviewComment_ProfileVersionId_ProfileSectionId]
		ON [dbo].[ReviewComment] ([ProfileVersionId], [ProfileSectionId])
END

GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.indexes
	WHERE [name] = N'IX_ReviewComment_ParentId'
		AND [object_id] = OBJECT_ID(N'[dbo].[ReviewComment]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_ReviewComment_ParentId]
		ON [dbo].[ReviewComment] ([ParentId])
END

GO
