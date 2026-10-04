/****** New stored procedure for CIDM external-user resolution: lookup by CidmSsoId. ******/
CREATE PROCEDURE [dbo].[spgUserByCidmSsoId]
	@CidmSsoId uniqueidentifier
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
		[CidmSsoId] = @CidmSsoId
END

GO
