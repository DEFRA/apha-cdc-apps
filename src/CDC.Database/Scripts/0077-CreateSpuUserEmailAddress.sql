-- Records the email address of an internal user the first time it is known, so a CIDM sign-in can later be matched to
-- the user by email. It only fills a missing address: an address already held is never replaced, and an address that
-- another user already holds is never assigned, so the call is safe to repeat on every sign-in. Returns 1 when the
-- address was recorded, otherwise 0. QUOTED_IDENTIFIER must be ON because [User] has filtered unique indexes.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spuUserEmailAddress]
	(
		@Id uniqueidentifier,
		@EmailAddress varchar(50)
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[EmailAddress] = @EmailAddress
	WHERE
		[Id] = @Id
		AND ([EmailAddress] IS NULL OR [EmailAddress] = '')
		AND NOT EXISTS (SELECT 1 FROM [User] WHERE [EmailAddress] = @EmailAddress AND [Id] <> @Id)

	SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS bit)
END

GO
