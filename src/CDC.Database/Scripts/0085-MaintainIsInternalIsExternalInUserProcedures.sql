-- Keeps [User].IsInternal and [User].IsExternal (0084) up to date, and lists the external users by IsExternal.
--
--   Maintain external users > Add
--     spiExternalUserPreRegistration           Creates the user with IsExternal = 1, so the user is listed straight away rather than after
--                                              their first CIDM sign-in. Otherwise unchanged from 0082.
--   Maintain external users > the list
--     spgaExternalUserMaintenance              Lists the users with IsExternal = 1, and their profile roles and section permissions (was
--     spuExternalUserSubscribedToReviewEmails  [SsoUserIdExt] IS NOT NULL). The review email subscription is saved only for such a user.
--                                              Otherwise unchanged from 0083.
--   Maintain global users > Add (the lookup)
--     spiGlobalUser                            The new optional @IsInternal = 1 records the user as internal, whether the user is a new row
--                                              or an existing user given a global role. An edit passes 0, which never clears the flag, and
--                                              the legacy site passes nothing, so it is unaffected. Everything else, including the
--                                              directory email address and object id, is unchanged from 0083-AddDirectoryDetailsToSpiGlobalUser,
--                                              so this script can be run with or without that one.
--   Sign-in: the procedures that record an identity set the flag with it, so a user who signs in is flagged whether or not the user
--   was added on one of the screens above
--     spuUserSsoUserIdExt, spiExternalUser     [SsoUserIdExt] set  ->  IsExternal = 1. Otherwise unchanged from 0058 and 0059.
--     spuUserSsoUserIdInt, spiInternalUser     [SsoUserIdInt] set  ->  IsInternal = 1. Otherwise unchanged from 0061 and 0073.
--
-- The Global users list (spgaInternalUser, 0083) is not changed: it still lists the users with [SsoUserIdInt] set.
--
-- Needs 0082 (spiExternalUserPreRegistration names LoginInstructionsSent) and 0084. Run 0086 after this script: until it has run, no
-- existing user has IsExternal = 1 and the External users list is empty.
--
-- QUOTED_IDENTIFIER and ANSI_NULLS must be ON when the procedures are created because [User] has filtered unique indexes, and sqlcmd
-- runs with QUOTED_IDENTIFIER OFF unless -I is given. CREATE OR ALTER keeps the existing EXECUTE permissions.
--
-- Safe to re-run. Run it before deploying the CDC.Api that passes @IsInternal.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

------------------------------------------------------------------------------------------------------------------------
-- 1. Maintain external users > Add: the new user is an external user
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spiExternalUserPreRegistration]
	(
		@Id uniqueidentifier,
		@UserName varchar(50),
		@FullName varchar(100),
		@Organisation varchar(100),
		@EmailAddress varchar(50)
	)
AS
BEGIN
	SET NOCOUNT ON;

	IF EXISTS (SELECT [Id] FROM [User] WHERE [EmailAddress] = @EmailAddress OR [UserName] = @UserName)
	BEGIN
		SELECT CAST(0 AS bit)
		RETURN
	END

	INSERT INTO [User]
		([Id], [UserName], [FullName], [Organisation], [EmailAddress], [SsoUserId], [IsProfileEditor], [IsPolicyProfileUser], [LoginInstructionsSent], [LoginInstructionsSentDate], [IsExternal])
	VALUES
		(@Id, @UserName, @FullName, @Organisation, @EmailAddress, NEWID(), 0, 0, 0, NULL, 1)

	SELECT CAST(1 AS bit)
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 2. Maintain external users > the list: the users, every profile, the users' profile roles and their section permissions
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spgaExternalUserMaintenance]
AS
BEGIN
	SET NOCOUNT ON;

	-- External users
	SELECT
		[User].[Id],
		[User].[UserName],
		[User].[FullName],
		ISNULL([User].[EmailAddress], '') AS [EmailAddress],
		[User].[Organisation],
		[User].[SubscribedToReviewEmails]
	FROM
		[User]
	WHERE
		[User].[IsExternal] = 1
	ORDER BY
		[User].[UserName]

	-- Profile Infos
	SELECT
		[Profile].[Id],
		CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END As Title
	FROM
		[Profile] LEFT JOIN
		[Profile] parentProfile ON [Profile].[ParentId] = parentProfile.[Id]
	ORDER BY
		dbo.udf_StripHTMLTags(CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END)

	-- External User Profiles
	SELECT
		[ProfileUser].[UserId],
		[Profile].[Id] as ProfileId,
		CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END As Title,
		[luProfileUserRole].[Id] AS RoleId,
		[luProfileUserRole].[Name] AS RoleName,
		[luProfileUserRole].[IsContributor]
	FROM
		[ProfileUser] INNER JOIN
		[User] ON [ProfileUser].[UserId] = [User].[Id] INNER JOIN
		[luProfileUserRole] ON [ProfileUser].[ProfileUserRoleId] = [luProfileUserRole].[Id] INNER JOIN
		[Profile] ON [Profile].[Id] = [ProfileUser].[ProfileId] LEFT JOIN
		[Profile] parentProfile ON [Profile].[ParentId] = parentProfile.[Id]
	WHERE
		[User].[IsExternal] = 1

	-- Section Permissions
	SELECT
		[ProfileSectionUser].[UserId],
		[ProfileSectionUser].[ProfileId],
		[ProfileSectionUser].[ProfileSectionId]
	FROM
		[ProfileSectionUser] INNER JOIN
		[User] ON [ProfileSectionUser].[UserId] = [User].[Id]
	WHERE
		[User].[IsExternal] = 1
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 3. Maintain external users > the list: the review email subscription
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuExternalUserSubscribedToReviewEmails]
	(
		@Id uniqueidentifier,
		@SubscribedToReviewEmails bit
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this UPDATE.
	UPDATE [User]
	SET
		[SubscribedToReviewEmails] = @SubscribedToReviewEmails
	WHERE
		[Id] = @Id
		AND [IsExternal] = 1
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 4. Maintain global users > Add (the lookup): the added user is an internal user
------------------------------------------------------------------------------------------------------------------------
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
	@SsoUserIdInt uniqueidentifier = NULL,
	@IsInternal bit = 0
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

		-- An existing user's flag is only ever set here, never cleared.
		UPDATE [User]
		SET
			[UserName] = @UserName,
			[FullName] = @FullName,
			[Organisation] = @Organisation,
			[SubscribedToReviewEmails] = @SubscribedToReviewEmails,
			[IsProfileEditor] = @IsProfileEditor,
			[IsPolicyProfileUser] = @IsPolicyProfileUser,
			[IsInternal] = CASE WHEN @IsInternal = 1 THEN 1 ELSE [IsInternal] END
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
			[SsoUserIdInt],
			[IsInternal]
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
			@SsoUserIdInt,
			ISNULL(@IsInternal, 0)
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

------------------------------------------------------------------------------------------------------------------------
-- 5. CIDM sign-in: a user linked to a CIDM identity is an external user
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuUserSsoUserIdExt]
	(
		@Id uniqueidentifier,
		@SsoUserIdExt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[SsoUserIdExt] = @SsoUserIdExt,
		[IsExternal] = 1
	WHERE
		[Id] = @Id
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spiExternalUser]
	(
		@Id uniqueidentifier,
		@UserName varchar(50),
		@FullName varchar(100),
		@Organisation varchar(100),
		@EmailAddress varchar(50),
		@SsoUserIdExt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	INSERT INTO [User]
		([Id], [UserName], [FullName], [Organisation], [EmailAddress], [SsoUserIdExt], [IsProfileEditor], [IsPolicyProfileUser], [IsExternal])
	VALUES
		(@Id, @UserName, @FullName, @Organisation, @EmailAddress, @SsoUserIdExt, 0, 0, 1)
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 6. Entra ID sign-in: a user linked to an Entra ID identity is an internal user
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuUserSsoUserIdInt]
	(
		@Id uniqueidentifier,
		@SsoUserIdInt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[SsoUserIdInt] = @SsoUserIdInt,
		[IsInternal] = 1
	WHERE
		[Id] = @Id
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spiInternalUser]
	(
		@Id uniqueidentifier,
		@UserName varchar(50),
		@FullName varchar(100),
		@SsoUserIdInt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	BEGIN TRY
		INSERT INTO [User]
			([Id], [UserName], [FullName], [Organisation], [IsProfileEditor], [IsPolicyProfileUser], [IsUserManagementSystem], [SsoUserIdInt], [IsInternal])
		VALUES
			(@Id, @UserName, @FullName, '', 0, 0, 0, @SsoUserIdInt, 1)
	END TRY
	BEGIN CATCH
		-- 2601/2627 on IX_User_SsoUserIdInt means another sign-in created this user first.
		IF NOT (ERROR_NUMBER() IN (2601, 2627) AND EXISTS (SELECT [Id] FROM [User] WHERE [SsoUserIdInt] = @SsoUserIdInt))
		BEGIN
			THROW;
		END
	END CATCH

	SELECT
		[Id],
		[UserName],
		[FullName],
		[IsProfileEditor],
		[IsPolicyProfileUser],
		[SsoUserIdInt]
	FROM
		[User]
	WHERE
		[SsoUserIdInt] = @SsoUserIdInt
END

GO
