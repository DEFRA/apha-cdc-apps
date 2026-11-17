-- Which users the Global users and External users screens list.
--
--   Global users   : [User].[SsoUserIdInt] IS NOT NULL  - a user linked to an Entra ID identity (set at their first Entra ID sign-in).
--   External users : [User].[SsoUserIdExt] IS NOT NULL  - a user linked to a CIDM identity (set at their first CIDM sign-in).
--
-- The legacy procedures the screens called decide differently (spgaGlobalUser: the user holds a global role;
-- spgaSsoUserMaintenance: the legacy [SsoUserId] is set). The legacy site still calls them, so they are left exactly as they
-- are and CDC.Api gets procedures of its own:
--
--   spgaInternalUser                         The Global users list. Same columns as spgaGlobalUser; it includes users who hold no
--                                            global role yet.
--   spgaExternalUserMaintenance              The External users list and the profile access of each. Same result sets as
--                                            spgaSsoUserMaintenance, for the new population; the first one no longer carries the
--                                            legacy [SsoUserId].
--   spuExternalUserSubscribedToReviewEmails  Saves the review email subscription by [Id]. spiSsoUser, which the page used before,
--                                            keys on the legacy [SsoUserId] and would add a second row for a user that has none.
--
-- A user is listed only once the column is filled in: an external user an administrator has pre-registered, or a global user an
-- administrator has added, is not listed until they have signed in.
--
-- QUOTED_IDENTIFIER and ANSI_NULLS must be ON when the procedures are created because [User] has filtered unique indexes, and
-- sqlcmd runs with QUOTED_IDENTIFIER OFF unless -I is given.
--
-- Safe to re-run. Run it before deploying the CDC.Api that calls these procedures.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

------------------------------------------------------------------------------------------------------------------------
-- 1. Global users
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spgaInternalUser]
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
		[LastUpdated]
	FROM
		[User]
	WHERE
		[SsoUserIdInt] IS NOT NULL
	ORDER BY
		[FullName]
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 2. External users: the users, every profile, the users' profile roles and their section permissions
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spgaExternalUserMaintenance]
AS
BEGIN
	SET NOCOUNT ON;

	-- External users
	SELECT
		[User].[Id],
		[User].[UserName],
		[User].[FullName],
		ISNULL([User].[EmailAddress], '') AS [EmailAddress],
		[User].[Organisation],
		[User].[SubscribedToReviewEmails]
	FROM
		[User]
	WHERE
		[User].[SsoUserIdExt] IS NOT NULL
	ORDER BY
		[User].[UserName]

	-- Profile Infos
	SELECT
		[Profile].[Id],
		CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END As Title
	FROM
		[Profile] LEFT JOIN
		[Profile] parentProfile ON [Profile].[ParentId] = parentProfile.[Id]
	ORDER BY
		dbo.udf_StripHTMLTags(CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END)

	-- External User Profiles
	SELECT
		[ProfileUser].[UserId],
		[Profile].[Id] as ProfileId,
		CASE WHEN [Profile].[ParentId] IS NULL THEN [Profile].[Title] ELSE parentProfile.[Title] + ' (' + [Profile].[ScenarioTitle] + ')' END As Title,
		[luProfileUserRole].[Id] AS RoleId,
		[luProfileUserRole].[Name] AS RoleName,
		[luProfileUserRole].[IsContributor]
	FROM
		[ProfileUser] INNER JOIN
		[User] ON [ProfileUser].[UserId] = [User].[Id] INNER JOIN
		[luProfileUserRole] ON [ProfileUser].[ProfileUserRoleId] = [luProfileUserRole].[Id] INNER JOIN
		[Profile] ON [Profile].[Id] = [ProfileUser].[ProfileId] LEFT JOIN
		[Profile] parentProfile ON [Profile].[ParentId] = parentProfile.[Id]
	WHERE
		[User].[SsoUserIdExt] IS NOT NULL

	-- Section Permissions
	SELECT
		[ProfileSectionUser].[UserId],
		[ProfileSectionUser].[ProfileId],
		[ProfileSectionUser].[ProfileSectionId]
	FROM
		[ProfileSectionUser] INNER JOIN
		[User] ON [ProfileSectionUser].[UserId] = [User].[Id]
	WHERE
		[User].[SsoUserIdExt] IS NOT NULL
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 3. External users: the review email subscription
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuExternalUserSubscribedToReviewEmails]
	(
		@Id uniqueidentifier,
		@SubscribedToReviewEmails bit
	)
AS
BEGIN
	SET NOCOUNT ON;

	-- LastUpdated is a timestamp/rowversion column: SQL Server bumps it automatically on this UPDATE.
	UPDATE [User]
	SET
		[SubscribedToReviewEmails] = @SubscribedToReviewEmails
	WHERE
		[Id] = @Id
		AND [SsoUserIdExt] IS NOT NULL
END

GO
