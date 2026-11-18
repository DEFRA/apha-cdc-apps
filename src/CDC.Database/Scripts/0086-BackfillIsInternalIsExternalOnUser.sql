-- One-time update of [User].IsInternal and [User].IsExternal (0084) for the users that exist when it is run:
--
--   IsInternal = 1   where [SsoUserIdInt] IS NOT NULL   - the user is linked to an Entra ID identity.
--   IsExternal = 1   where [SsoUserIdExt] IS NOT NULL   - the user is linked to a CIDM identity.
--
-- Run it once per environment, after 0084 and 0085. The Maintain external users list is the users with IsExternal = 1, so it is empty
-- until this has run. A flag is only ever set here, never cleared, so running it again changes nothing that is already right.
--
-- QUOTED_IDENTIFIER must be ON because [User] has filtered unique indexes, and sqlcmd runs with it OFF unless -I is given.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

UPDATE [User]
SET
	[IsInternal] = 1
WHERE
	[SsoUserIdInt] IS NOT NULL
	AND [IsInternal] = 0

GO

UPDATE [User]
SET
	[IsExternal] = 1
WHERE
	[SsoUserIdExt] IS NOT NULL
	AND [IsExternal] = 0

GO
