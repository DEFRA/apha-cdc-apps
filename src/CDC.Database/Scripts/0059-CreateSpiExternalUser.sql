/****** New stored procedure for CIDM external-user resolution: provisions a brand-new User row
        the first time someone signs in through CIDM and no existing row matches by SsoUserIdExt or
        email. UserName is set to the email address for new records, per the agreed convention. ******/
CREATE OR ALTER PROCEDURE [dbo].[spiExternalUser]
	(
		@Id uniqueidentifier,
		@UserName varchar(50),
		@FullName varchar(100),
		@Organisation varchar(100),
		@EmailAddress varchar(50),
		@SsoUserIdExt uniqueidentifier
	)
AS
BEGIN
	SET NOCOUNT ON;

	INSERT INTO [User]
		([Id], [UserName], [FullName], [Organisation], [EmailAddress], [SsoUserIdExt], [IsProfileEditor], [IsPolicyProfileUser])
	VALUES
		(@Id, @UserName, @FullName, @Organisation, @EmailAddress, @SsoUserIdExt, 0, 0)
END

GO
