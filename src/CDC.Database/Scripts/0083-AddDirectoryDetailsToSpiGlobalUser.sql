-- Records the directory email address and Entra ID object id of a global user when the user is created from the directory search
-- (Maintain global users > Add). Both parameters are optional, so existing callers are unaffected.
--
-- Only the INSERT branch uses them: an existing user is never changed here, because a sign-in has already recorded
-- SsoUserIdInt and EmailAddress for it, or an administrator has chosen to leave them. A value that another user already
-- holds is left out rather than failing the add - EmailAddress is how a CIDM sign-in finds a user, and SsoUserIdInt has a
-- unique index (IX_User_SsoUserIdInt). Everything else is unchanged from 0071.
-- QUOTED_IDENTIFIER must be ON because [User] has filtered unique indexes.
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
	@NewLastUpdated timestamp OUTPUT,
	@EmailAddress varchar(50) = NULL,
	@SsoUserIdInt uniqueidentifier = NULL
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

		SET @EmailAddress = NULLIF(LTRIM(RTRIM(@EmailAddress)), '')

		IF @EmailAddress IS NOT NULL AND EXISTS (SELECT 1 FROM [User] WHERE [EmailAddress] = @EmailAddress) BEGIN
			SET @EmailAddress = NULL
		END

		IF @SsoUserIdInt IS NOT NULL AND EXISTS (SELECT 1 FROM [User] WHERE [SsoUserIdInt] = @SsoUserIdInt) BEGIN
			SET @SsoUserIdInt = NULL
		END

		INSERT INTO [User]
			(
			[Id],
			[UserName],
			[FullName],
			[Organisation],
			[SubscribedToReviewEmails],
			[IsProfileEditor],
			[IsPolicyProfileUser],
			[EmailAddress],
			[SsoUserIdInt]
			)
		VALUES
			(
			@UserId,
			@UserName,
			@FullName,
			@Organisation,
			@SubscribedToReviewEmails,
			@IsProfileEditor,
			@IsPolicyProfileUser,
			@EmailAddress,
			@SsoUserIdInt
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
