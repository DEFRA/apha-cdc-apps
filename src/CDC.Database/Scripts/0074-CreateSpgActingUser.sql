-- Everything CDC.Api needs to authorise one request, in one round trip: the acting user's role flags and sign-on
-- identifiers (SsoUserIdInt set = an internal Entra ID user, SsoUserIdExt set = an external CIDM user), the profiles
-- the user holds a role on, and the profile sections the user may contribute to. It replaces spgUser for the
-- acting-user lookup, which returned the flags only.
-- Result sets: 1 the user (empty when the id is unknown), 2 profile roles, 3 section assignments. CDC.Api ignores
-- the second and third for an internal user and the flags for an external user.
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[spgActingUser]
	@UserId uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		[Id],
		[IsProfileEditor],
		[IsPolicyProfileUser],
		[SsoUserIdInt],
		[SsoUserIdExt]
	FROM
		[User]
	WHERE
		[Id] = @UserId

	SELECT
		[ProfileUser].[ProfileId],
		[ProfileUser].[ProfileUserRoleId] AS [RoleId],
		[luProfileUserRole].[Name] AS [RoleName],
		[luProfileUserRole].[IsContributor]
	FROM
		[ProfileUser] INNER JOIN [luProfileUserRole] ON [ProfileUser].[ProfileUserRoleId] = [luProfileUserRole].[Id]
	WHERE
		[ProfileUser].[UserId] = @UserId

	SELECT
		[ProfileId],
		[ProfileSectionId]
	FROM
		[ProfileSectionUser]
	WHERE
		[UserId] = @UserId
END

GO
