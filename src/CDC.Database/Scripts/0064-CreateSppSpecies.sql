/****** Creates sppSpecies, used by Maintain species data's "Inactivate" action. Ports the
        legacy procedure unchanged: checks the row version, writes a SpeciesTableAuditLog entry
        recording '- to be inactivated -' as the new name/parent, then sets EffectiveDateTo so
        the species no longer appears as active, while its data and hierarchy relationships
        are retained. ******/
CREATE OR ALTER PROCEDURE [dbo].[sppSpecies]
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
				RAISERROR('The species cannot be inactivated because it has been edited by another user',16,1)
				RETURN
			END

	DECLARE @OldParentId uniqueidentifier
	DECLARE @OldName varchar(50)
	SELECT
		@OldParentId = [ParentId],
		@OldName = [Name]
	FROM
		[dbo].[Species]
	WHERE
		[Id]=@SpeciesId

	DECLARE @OldParent varchar(50)
	IF @OldParentId IS NULL
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
			[Id]=@OldParentId
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
		'- to be inactivated -',
		@OldParent,
		'- to be inactivated -'
		)

	-- Update Species
	UPDATE
		[Species]
	SET
		[EffectiveDateTo] = @EffectiveDate
	WHERE
		[Id] = @SpeciesId

END

GO
