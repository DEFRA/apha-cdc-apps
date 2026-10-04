/****** New stored procedure for CIDM external-user resolution: lookup by email, used only as a
        fallback the first time a user signs in through CIDM and has no CidmSsoId recorded yet. ******/
CREATE PROCEDURE [dbo].[spgUserByEmailAddress]
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
		[CidmSsoId],
		[EmailAddress],
		[LastUpdated]
	FROM
		[User]
	WHERE
		[EmailAddress] = @EmailAddress
END

GO
