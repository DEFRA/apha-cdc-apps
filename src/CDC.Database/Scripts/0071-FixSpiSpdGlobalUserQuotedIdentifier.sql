-- spiGlobalUser/spdGlobalUser were created with QUOTED_IDENTIFIER OFF (the setting at creation is
-- stored with the procedure), so their UPDATE of [User] now fails (error 1934) because [User] has
-- filtered unique indexes (IX_User_SsoUserIdExt / IX_User_SsoUserIdInt) that require it ON.
-- Logic is unchanged from the legacy procedures (0015-UpdateInsertGlobalUserSpg.sql / spdGlobalUser).
-- CREATE OR ALTER keeps the existing EXECUTE permissions.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spiGlobalUser]
	@UserId uniqueidentifier,
	@UserName varchar(50),
	@FullName varchar(100),
	@Organisation varchar(100),
	@SubscribedToReviewEmails bit,
	@IsProfileEditor bit,
	@IsPolicyProfileUser bit,
	@LastUpdated timestamp,
	@NewLastUpdated timestamp OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	IF @IsProfileEditor = @IsPolicyProfileUser BEGIN
		RAISERROR('You cannot set the Profile Editor flag and the Policy Profile User flag to the same setting', 16, 1)
		RETURN
	END

	IF EXISTS (SELECT [Id] FROM [User] WHERE [Id] = @UserId)
	BEGIN

		IF NOT EXISTS (SELECT [Id] FROM [User] WHERE [Id] = @UserId AND [LastUpdated] = @LastUpdated) BEGIN
			RAISERROR('The User information has been edited by another user', 16, 1)
			RETURN
		END

		UPDATE [User]
		SET
			[UserName] = @UserName,
			[FullName] = @FullName,
			[Organisation] = @Organisation,
			[SubscribedToReviewEmails] = @SubscribedToReviewEmails,
			[IsProfileEditor] = @IsProfileEditor,
			[IsPolicyProfileUser] = @IsPolicyProfileUser
		WHERE
			[Id] = @UserId

	END ELSE BEGIN

		IF EXISTS (SELECT [Id] FROM [User] WHERE [UserName] = @UserName) BEGIN
			RAISERROR('There is already a user with the specified username', 16, 1)
			RETURN
		END

		INSERT INTO [User]
			(
			[Id],
			[UserName],
			[FullName],
			[Organisation],
			[SubscribedToReviewEmails],
			[IsProfileEditor],
			[IsPolicyProfileUser]
			)
		VALUES
			(
			@UserId,
			@UserName,
			@FullName,
			@Organisation,
			@SubscribedToReviewEmails,
			@IsProfileEditor,
			@IsPolicyProfileUser
			)
	END

	SELECT
		@NewLastUpdated = [LastUpdated]
	FROM
		[User]
	WHERE
		[Id] = @UserId
END
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spdGlobalUser]
	@UserId uniqueidentifier,
	@LastUpdated timestamp,
	@NewLastUpdated timestamp OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	IF NOT EXISTS (SELECT [Id] FROM [User] WHERE [Id] = @UserId AND [LastUpdated] = @LastUpdated) BEGIN
		RAISERROR('The User information has been edited by another user', 16, 1)
		RETURN
	END

	UPDATE [User]
	SET
		[IsProfileEditor] = 0,
		[IsPolicyProfileUser] = 0
	WHERE
		[Id] = @UserId

	SELECT
		@NewLastUpdated = [LastUpdated]
	FROM
		[User]
	WHERE
		[Id] = @UserId
END
GO
