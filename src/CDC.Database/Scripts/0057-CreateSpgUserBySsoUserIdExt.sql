-- New stored procedure for CIDM external-user resolution: lookup by SsoUserIdExt.
CREATE OR ALTER PROCEDURE [dbo].[spgUserBySsoUserIdExt]
	@SsoUserIdExt uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[Id],
		[UserName],
		[FullName],
		[Organisation],
		[SubscribedToReviewEmails],
		[IsProfileEditor],
		[IsPolicyProfileUser],
		[SsoUserId],
		[SsoUserIdExt],
		[EmailAddress],
		[LastUpdated]
	FROM
		[User]
	WHERE
		[SsoUserIdExt] = @SsoUserIdExt
END

GO
