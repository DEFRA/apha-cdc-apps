/****** New stored procedure for CIDM external-user resolution: backfills SsoUserIdExt the first
        time a user matched by email signs in through CIDM, so every subsequent sign-in matches
        directly on SsoUserIdExt instead. ******/
CREATE OR ALTER PROCEDURE [dbo].[spuUserSsoUserIdExt]
	(
		@Id uniqueidentifier,
		@SsoUserIdExt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[SsoUserIdExt] = @SsoUserIdExt
	WHERE
		[Id] = @Id
END

GO
