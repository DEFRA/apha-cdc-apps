/****** New stored procedure for Entra ID internal-user resolution: lookup by user name, used only
        as a fallback the first time a user signs in through Entra ID and has no SsoUserIdInt
        recorded yet. ******/
CREATE OR ALTER PROCEDURE [dbo].[spgUserByUserName]
	@UserName varchar(50)
AS
BEGIN
	SET NOCOUNT ON;

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
		[UserName] = @UserName
END

GO
