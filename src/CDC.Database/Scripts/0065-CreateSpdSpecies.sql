/****** Creates spdSpecies, used by Maintain species data's "Delete" action. Ports the legacy
        procedure unchanged: checks the row version, writes a SpeciesTableAuditLog entry recording
        '- to be deleted -' as the new name/parent, decrements the sequence number of every later
        sibling, then deletes the species' field values, prioritisation scores and species row.
        Business rules (species must be active, not in use by a profile, and have no children) are
        enforced in C# before this procedure is called, exactly as the legacy CSLA business object
        did - this procedure itself performs no such checks. ******/
CREATE OR ALTER PROCEDURE [dbo].[spdSpecies]
	@SpeciesId uniqueidentifier,
	@UserId uniqueidentifier,
	@Reason varchar(255),
	@LastUpdated timestamp
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @EffectiveDate smalldatetime
	SET @EffectiveDate = GetDate()

	-- Check timestamp is valid
	IF NOT EXISTS (
		SELECT
			[Id]
		FROM
			[Species]
		WHERE
			[Id] = @SpeciesId
			AND [LastUpdated] = @LastUpdated
		)
			BEGIN
				RAISERROR('The species cannot be deleted because it has been edited by another user',16,1)
				RETURN
			END

	DECLARE @OldName varchar(50)
	DECLARE @ParentId uniqueidentifier
	DECLARE @SequenceNumber int
	SELECT
		@OldName = [Name],
		@ParentId = [ParentId],
		@SequenceNumber = [SequenceNumber]
	FROM
		[dbo].[Species]
	WHERE
		[Id]=@SpeciesId


	DECLARE @OldParent varchar(50)
	IF @ParentId IS NULL
	BEGIN
		SET @OldParent = '- root species -'
	END
	ELSE
	BEGIN
		SELECT
			@OldParent = [Name]
		FROM
			[dbo].[Species]
		WHERE
			[Id]=@ParentId
	END

	--Insert Audit (parent)
	INSERT INTO [SpeciesTableAuditLog]
		(
		[Id],
		[UserId],
		[EffectiveDate],
		[LogDate],
		[Reason],
		[OldName],
		[NewName],
		[OldParent],
		[NewParent]
		)
	VALUES
		(
		newid(),
		@UserId,
		@EffectiveDate,
		@EffectiveDate,
		@Reason,
		@OldName,
		'- to be deleted -',
		@OldParent,
		'- to be deleted -'
		)


	DECLARE @EmptyGuid uniqueidentifier
	SET @EmptyGuid = '00000000-0000-0000-0000-000000000000'

	UPDATE
		[Species]
	SET
		[SequenceNumber] = [SequenceNumber] - 1
	WHERE
		ISNULL([ParentId], @EmptyGuid) = ISNULL(@ParentId, @EmptyGuid) AND
		[SequenceNumber] > @SequenceNumber

	-- Delete Species
	DELETE FROM
		[SpeciesFieldValue]
	WHERE
		[SpeciesId] = @SpeciesId

	DELETE FROM
		[SpeciesPrioritisationScore]
	WHERE
		[SpeciesId] = @SpeciesId

	DELETE FROM
		[Species]
	WHERE
		[Id] = @SpeciesId

END

GO
