/****** Further alters spuSpeciesSequenceNumber (on top of 0063) so moving a species up/down
        finds its actual nearest sibling by SequenceNumber order, rather than assuming the
        immediately adjacent integer (old +/- 1) is occupied. Legacy data can have gaps in
        SequenceNumber within a sibling group (for example 9, then 11 with no 10), which made the
        old +/- 1 lookup fail with "There is no species above/below this one" even when a real
        neighbour existed - reproduced by moving "Fish - other" down. Swapping with the nearest
        neighbour by order (rather than by adjacent integer) fixes this while leaving the
        contiguous case - and the audit trail entry from 0063 - unchanged. ******/
CREATE OR ALTER PROCEDURE [dbo].[spuSpeciesSequenceNumber]
	@SpeciesId uniqueidentifier,
	@IsMovingUp bit,
	@UserId uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @OldSequenceNumber int
	DECLARE @NewSequenceNumber int
	DECLARE @ParentId uniqueidentifier
	DECLARE @EmptyGuid uniqueidentifier
	DECLARE @NextSpeciesId uniqueidentifier

	SET @EmptyGuid = '00000000-0000-0000-0000-000000000000'

	SELECT
		@ParentId = [ParentId]
	FROM
		[Species]
	WHERE
		[Species].[Id] = @SpeciesId

	SELECT
		@OldSequenceNumber = [SequenceNumber]
	FROM
		[Species]
	WHERE
		[Id] = @SpeciesId

	-- Finds the nearest sibling by SequenceNumber order rather than assuming old +/- 1 is
	-- occupied, so a gap in the sequence (legacy data) does not block a move that should succeed.
	IF @IsMovingUp = 1
	BEGIN
		SELECT TOP (1)
			@NextSpeciesId = [Id],
			@NewSequenceNumber = [SequenceNumber]
		FROM
			[Species]
		WHERE
			ISNULL([ParentId], @EmptyGuid) = ISNULL(@ParentId, @EmptyGuid) AND
			[SequenceNumber] < @OldSequenceNumber
		ORDER BY
			[SequenceNumber] DESC
	END
	ELSE
	BEGIN
		SELECT TOP (1)
			@NextSpeciesId = [Id],
			@NewSequenceNumber = [SequenceNumber]
		FROM
			[Species]
		WHERE
			ISNULL([ParentId], @EmptyGuid) = ISNULL(@ParentId, @EmptyGuid) AND
			[SequenceNumber] > @OldSequenceNumber
		ORDER BY
			[SequenceNumber] ASC
	END

	IF @NextSpeciesId IS NULL
	BEGIN
		RAISERROR('Sequence change failed: There is no species above/below this one', 16, 1)
		RETURN
	END

	UPDATE
		[Species]
	SET
		[SequenceNumber] = @NewSequenceNumber
	WHERE
		[Id] = @SpeciesId

	UPDATE
		[Species]
	SET
		[SequenceNumber] = @OldSequenceNumber
	WHERE
		[Id] = @NextSpeciesId

	DECLARE @Name varchar(50)
	DECLARE @ParentName varchar(50)

	SELECT
		@Name = [Name]
	FROM
		[Species]
	WHERE
		[Id] = @SpeciesId

	IF @ParentId IS NULL
	BEGIN
		SET @ParentName = '- root species -'
	END
	ELSE
	BEGIN
		SELECT
			@ParentName = [Name]
		FROM
			[Species]
		WHERE
			[Id] = @ParentId
	END

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
		NEWID(),
		@UserId,
		GETDATE(),
		GETDATE(),
		CASE WHEN @IsMovingUp = 1 THEN 'Moved up within the hierarchy' ELSE 'Moved down within the hierarchy' END,
		@Name,
		@Name,
		@ParentName,
		@ParentName
		)
END

GO
