/****** New stored procedure for Entra ID internal-user resolution: backfills SsoUserIdInt the first
        time a user matched by user name signs in through Entra ID, so every subsequent sign-in
        matches directly on SsoUserIdInt instead. ******/
CREATE OR ALTER PROCEDURE [dbo].[spuUserSsoUserIdInt]
	(
		@Id uniqueidentifier,
		@SsoUserIdInt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[SsoUserIdInt] = @SsoUserIdInt
	WHERE
		[Id] = @Id
END

GO
