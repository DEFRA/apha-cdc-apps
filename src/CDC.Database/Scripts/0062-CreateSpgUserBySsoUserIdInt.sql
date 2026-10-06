-- New stored procedure for Entra ID internal-user resolution: lookup by SsoUserIdInt.
CREATE OR ALTER PROCEDURE [dbo].[spgUserBySsoUserIdInt]
	@SsoUserIdInt uniqueidentifier
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
		[SsoUserIdInt] = @SsoUserIdInt
END

GO
