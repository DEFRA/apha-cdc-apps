-- Adds [SsoUserIdInt] to the result of spgUserByEmailAddress so CDC.Api can tell that an email address
-- belongs to an internal (Entra ID) user, who may sign in through CIDM without a CIDM id being stored.
CREATE OR ALTER PROCEDURE [dbo].[spgUserByEmailAddress]
	@EmailAddress varchar(50)
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
		[SsoUserIdInt],
		[SsoUserIdExt],
		[EmailAddress],
		[LastUpdated]
	FROM
		[User]
	WHERE
		[EmailAddress] = @EmailAddress
END

GO
