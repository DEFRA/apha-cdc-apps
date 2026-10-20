-- Entra ID internal-user resolution: provisions a [User] row the first time someone signs in through
-- Entra ID and no existing row matches by SsoUserIdInt or user name. The row holds no privileges
-- (IsProfileEditor, IsPolicyProfileUser and IsUserManagementSystem are all 0) until a role is granted
-- on the Global users page. QUOTED_IDENTIFIER must be ON because [User] has filtered unique indexes.
-- A concurrent first sign-in that already created the row for this SsoUserIdInt is not an error: the
-- row it created is returned.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
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
			([Id], [UserName], [FullName], [Organisation], [IsProfileEditor], [IsPolicyProfileUser], [IsUserManagementSystem], [SsoUserIdInt])
		VALUES
			(@Id, @UserName, @FullName, '', 0, 0, 0, @SsoUserIdInt)
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
