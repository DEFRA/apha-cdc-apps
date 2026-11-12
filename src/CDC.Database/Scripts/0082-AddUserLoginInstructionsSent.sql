-- Login instructions for external users (Maintain external users "Login instructions" column).
--
-- [User].LoginInstructionsSent        bit NOT NULL      True once the login instructions email has been sent to the saved address.
-- [User].LoginInstructionsSentDate    datetime2 NULL    When it was sent (null for users that existed before this script).
--
-- The column defaults to SENT (1): every user that already exists, and every user created by a path that does not name the
-- column (internal users, CIDM first sign-in), needs no instructions and shows "Sent". Only a newly pre-registered external
-- user is created as NOT SENT (0) - spiExternalUserPreRegistration below - so the page offers "Send instructions" for them.
-- The flag only matters for external users ([SsoUserId] IS NOT NULL).
--
-- Changing a user's saved email address clears the flag again, and ends the user's CIDM link ([SsoUserIdExt]), in trigger
-- TR_User_EmailAddressChanged. The page then offers "Send instructions" for the new address, and the next CIDM sign-in
-- links the new CIDM account by email (spgUserByEmailAddress / spuUserSsoUserIdExt). Without this, an administrator who
-- corrected an address would leave the old CIDM account linked, so its owner could still sign in. A trigger is used because the
-- address can be changed by procedures that are not in this repository (for example spiSsoUser); every such path is covered
-- without redefining them. [SsoUserId] (what marks the row as external) is never touched.
--
-- QUOTED_IDENTIFIER and ANSI_NULLS must be ON when the procedures and the trigger are created because [User] has filtered
-- unique indexes, and sqlcmd runs with QUOTED_IDENTIFIER OFF unless -I is given.
--
-- Safe to re-run.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

------------------------------------------------------------------------------------------------------------------------
-- 1. Columns: existing rows (and rows inserted without naming the column) are Sent
------------------------------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'[dbo].[User]', N'LoginInstructionsSent') IS NULL
BEGIN
	ALTER TABLE [dbo].[User] ADD
		[LoginInstructionsSent] BIT NOT NULL CONSTRAINT [DF_User_LoginInstructionsSent] DEFAULT (1),
		[LoginInstructionsSentDate] DATETIME2 NULL
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 2. Pre-registration creates the user as NOT SENT. Otherwise unchanged from 0075.
------------------------------------------------------------------------------------------------------------------------
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
		([Id], [UserName], [FullName], [Organisation], [EmailAddress], [SsoUserId], [IsProfileEditor], [IsPolicyProfileUser], [LoginInstructionsSent], [LoginInstructionsSentDate])
	VALUES
		(@Id, @UserName, @FullName, @Organisation, @EmailAddress, NEWID(), 0, 0, 0, NULL)

	SELECT CAST(1 AS bit)
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 3. A changed email address on an external user clears the sent flag and the CIDM link.
--    UPDATE([EmailAddress]) is true when the column is in the SET list, so the old and new values are compared: setting
--    the same address (or changing only its letter case, under the column's collation) clears nothing. Filling in a missing
--    address counts as a change.
------------------------------------------------------------------------------------------------------------------------
-- An earlier draft of this script named the trigger after the sent flag alone.
DROP TRIGGER IF EXISTS [dbo].[TR_User_ResetLoginInstructionsOnEmailChange]

GO

CREATE OR ALTER TRIGGER [dbo].[TR_User_EmailAddressChanged]
ON [dbo].[User]
AFTER UPDATE
AS
BEGIN
	SET NOCOUNT ON;

	IF NOT UPDATE([EmailAddress])
	BEGIN
		RETURN
	END

	UPDATE u
	SET
		[LoginInstructionsSent] = 0,
		[LoginInstructionsSentDate] = NULL,
		[SsoUserIdExt] = NULL
	FROM
		[User] u
		INNER JOIN inserted i ON i.[Id] = u.[Id]
		INNER JOIN deleted d ON d.[Id] = u.[Id]
	WHERE
		u.[SsoUserId] IS NOT NULL
		AND ISNULL(i.[EmailAddress], '') <> ISNULL(d.[EmailAddress], '')
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 4. Procedures behind the Send instructions button
------------------------------------------------------------------------------------------------------------------------
-- The external users still waiting for their instructions; the list shows "Sent" for every other external user.
CREATE OR ALTER PROCEDURE [dbo].[spgaExternalUsersAwaitingLoginInstructions]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[Id]
	FROM
		[User]
	WHERE
		[SsoUserId] IS NOT NULL
		AND [LoginInstructionsSent] = 0
END

GO

-- One external user's name, saved address and sent flag; no row when the user is not an external user.
CREATE OR ALTER PROCEDURE [dbo].[spgExternalUserLoginInstructions]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[Id],
		[FullName],
		[EmailAddress],
		[LoginInstructionsSent]
	FROM
		[User]
	WHERE
		[Id] = @Id
		AND [SsoUserId] IS NOT NULL
END

GO

-- Records that the instructions were emailed to @EmailAddress. Only applies while that is still the saved address, so an
-- address changed during the send leaves the user unsent. Returns 1 when recorded, otherwise 0.
CREATE OR ALTER PROCEDURE [dbo].[spuExternalUserLoginInstructionsSent]
	(
		@Id uniqueidentifier,
		@EmailAddress varchar(50),
		@SentDate datetime2
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this UPDATE.
	UPDATE [User]
	SET
		[LoginInstructionsSent] = 1,
		[LoginInstructionsSentDate] = @SentDate
	WHERE
		[Id] = @Id
		AND [SsoUserId] IS NOT NULL
		AND [EmailAddress] = @EmailAddress

	SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS bit)
END

GO
