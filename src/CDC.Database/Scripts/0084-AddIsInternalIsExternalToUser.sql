-- Marks which users are internal and which are external, for the Maintain global users and Maintain external users screens.
--
-- [User].IsInternal   bit NOT NULL   1 = an internal user: added on Maintain global users, or signed in through Entra ID.
-- [User].IsExternal   bit NOT NULL   1 = an external user: added on Maintain external users, or signed in through CIDM.
--
-- Both default to 0, so a row created by a path that does not name them - the legacy site, for one - is neither until a flag is set.
-- Run 0085 next (it sets the flags from then on), then 0086 (it sets them for the users that exist now).
--
-- Safe to re-run.
IF COL_LENGTH(N'[dbo].[User]', N'IsInternal') IS NULL
BEGIN
	ALTER TABLE [dbo].[User] ADD
		[IsInternal] BIT NOT NULL CONSTRAINT [DF_User_IsInternal] DEFAULT (0)
END

GO

IF COL_LENGTH(N'[dbo].[User]', N'IsExternal') IS NULL
BEGIN
	ALTER TABLE [dbo].[User] ADD
		[IsExternal] BIT NOT NULL CONSTRAINT [DF_User_IsExternal] DEFAULT (0)
END

GO
