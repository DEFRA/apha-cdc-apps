/****** Alters spuSpeciesSequenceNumber so reordering a species via Maintain species data writes
        a SpeciesTableAuditLog entry (old/new name and parent are unchanged; the move direction
        is recorded in Reason), matching the audit trail already written for add/rename/
        inactivate/delete. The legacy procedure never recorded this. ******/
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

	IF @IsMovingUp = 1
	BEGIN
		SET @NewSequenceNumber = @OldSequenceNumber - 1
	END
	ELSE
	BEGIN
		SET @NewSequenceNumber = @OldSequenceNumber + 1
	END

	IF NOT EXISTS
	(
		SELECT
			[Id]
		FROM
			[Species]
		WHERE
			ISNULL([ParentId], @EmptyGuid) = ISNULL(@ParentId, @EmptyGuid) AND
			[SequenceNumber] = @NewSequenceNumber
	)
	BEGIN
		RAISERROR('Sequence change failed: There is no species above/below this one', 16, 1)
		RETURN
	END

	DECLARE @NextSpeciesId uniqueidentifier

	SELECT
		@NextSpeciesId = [Id]
	FROM
		[Species]
	WHERE
		ISNULL([ParentId], @EmptyGuid) = ISNULL(@ParentId, @EmptyGuid) AND
		[SequenceNumber] = @NewSequenceNumber

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
