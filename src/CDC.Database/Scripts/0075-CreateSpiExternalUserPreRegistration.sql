-- Pre-registers an external user so they can sign in through CIDM (self-registration is blocked).
-- SsoUserId is set to a new value because that is what marks a [User] row as external; SsoUserIdExt stays
-- NULL until the user's first CIDM sign-in links it by email. UserName is the email address, per the
-- agreed convention. Returns 1 when the user was created, or 0 when the email address or user name is
-- already in use. QUOTED_IDENTIFIER must be ON because [User] has filtered unique indexes.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

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
		([Id], [UserName], [FullName], [Organisation], [EmailAddress], [SsoUserId], [IsProfileEditor], [IsPolicyProfileUser])
	VALUES
		(@Id, @UserName, @FullName, @Organisation, @EmailAddress, NEWID(), 0, 0)

	SELECT CAST(1 AS bit)
END

GO
