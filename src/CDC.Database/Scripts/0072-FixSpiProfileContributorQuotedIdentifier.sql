-- spiProfileContributor was created with QUOTED_IDENTIFIER OFF (the setting at creation is stored
-- with the procedure), so its UPDATE of [User] fails (error 1934) because [User] has filtered unique
-- indexes (IX_User_SsoUserIdExt / IX_User_SsoUserIdInt) that require it ON. This breaks granting an
-- external user access to a profile. Logic is unchanged from the legacy procedure.
-- CREATE OR ALTER keeps the existing EXECUTE permissions.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spiProfileContributor]
	@UserId uniqueidentifier,
	@UserName varchar(50),
	@FullName varchar(100),
	@Organisation varchar(100),
	@RoleId uniqueidentifier,
	@ProfileId uniqueidentifier,
	@LastUpdated timestamp,
	@NewLastUpdated timestamp OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	IF EXISTS (SELECT [Id] FROM [User] WHERE [Id] = @UserId)
	BEGIN

		IF NOT EXISTS (SELECT [Id] FROM [User] WHERE [Id] = @UserId AND [LastUpdated] = @LastUpdated) BEGIN
			RAISERROR('The user information has been edited by another user', 16, 1)
			RETURN
		END

		IF EXISTS (SELECT * FROM [dbo].[User] WHERE [Id] = @UserId AND [SsoUserId] IS NULL)
		BEGIN
			UPDATE [User]
			SET
				[UserName] = @UserName,
				[FullName] = @FullName,
				[Organisation] = @Organisation
			WHERE
				[Id] = @UserId
		END

		IF EXISTS (SELECT [ProfileUserRoleId] FROM [ProfileUser] WHERE [UserId] = @UserId AND [ProfileId] = @ProfileId)
		BEGIN
			UPDATE [ProfileUser]
			SET
				[ProfileUserRoleId] = @RoleId
			WHERE
				[UserId] = @UserId
				AND [ProfileId] = @ProfileId
		END ELSE BEGIN
			INSERT INTO [ProfileUser] ([UserId], [ProfileId], [ProfileUserRoleId])
			VALUES (@UserId, @ProfileId, @RoleId)
		END

	END ELSE BEGIN

		IF EXISTS (SELECT [Id] FROM [User] WHERE [UserName] = @UserName) BEGIN
			RAISERROR('There is already a user with the specified username', 16, 1)
			RETURN
		END

		INSERT INTO [User]
			([Id], [UserName], [FullName], [Organisation], [IsProfileEditor], [IsPolicyProfileUser])
		VALUES
			(@UserId, @UserName, @FullName, @Organisation, 0, 0)

		INSERT INTO [ProfileUser] ([UserId], [ProfileId], [ProfileUserRoleId])
		VALUES (@UserId, @ProfileId, @RoleId)
	END

	SELECT
		@NewLastUpdated = [LastUpdated]
	FROM
		[User]
	WHERE
		[Id] = @UserId
END
GO
