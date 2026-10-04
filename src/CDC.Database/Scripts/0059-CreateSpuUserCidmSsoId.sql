/****** New stored procedure for CIDM external-user resolution: backfills CidmSsoId the first
        time a user matched by email signs in through CIDM, so every subsequent sign-in matches
        directly on CidmSsoId instead. ******/
CREATE PROCEDURE [dbo].[spuUserCidmSsoId]
	(
		@Id uniqueidentifier,
		@CidmSsoId uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this
	-- UPDATE and rejects any attempt to assign it explicitly.
	UPDATE [User]
	SET
		[CidmSsoId] = @CidmSsoId
	WHERE
		[Id] = @Id
END

GO
