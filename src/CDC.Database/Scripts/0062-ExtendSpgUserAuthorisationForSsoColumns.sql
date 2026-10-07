-- Reuses the legacy app's existing spgUserAuthorisation procedure (the one it already calls on
-- every authenticated request) for CDC's user resolution too, instead of maintaining parallel
-- lookup procedures. Adds the two new SSO columns as additional, optional lookup keys and appends
-- them - plus EmailAddress, which CDC's external-user flow needs but this procedure never
-- selected before - to the END of the result columns, so none of the legacy app's existing
-- ordinal-based column reads (SafeDataReader.GetGuid(0), GetString(5), etc.) shift.
--
-- Parameter priority when more than one is supplied: SsoUserIdExt, then SsoUserIdInt, then
-- UserName, then the legacy SsoUserId - matching the existing precedent that only one of
-- UserName/SsoUserId was ever supplied per call. Both new parameters default to NULL, so the
-- legacy app's two existing call patterns (UserName-only, or SsoUserId-only) are unaffected.
CREATE OR ALTER PROCEDURE [dbo].[spgUserAuthorisation]
	@UserName varchar(50) = NULL,
	@SsoUserId uniqueidentifier = NULL,
	@SsoUserIdExt uniqueidentifier = NULL,
	@SsoUserIdInt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @UserId uniqueidentifier

	IF @SsoUserIdExt IS NOT NULL BEGIN

		SELECT
			@UserId = [Id]
		FROM
			[User]
		WHERE
			[SsoUserIdExt] = @SsoUserIdExt

	END ELSE IF @SsoUserIdInt IS NOT NULL BEGIN

		SELECT
			@UserId = [Id]
		FROM
			[User]
		WHERE
			[SsoUserIdInt] = @SsoUserIdInt

	END ELSE IF @UserName IS NOT NULL BEGIN

		SELECT
			@UserId = [Id]
		FROM
			[User]
		WHERE
			[UserName] = @UserName

	END ELSE BEGIN

		SELECT
			@UserId = [Id]
		FROM
			[User]
		WHERE
			[SsoUserId] = @SsoUserId

	END

	SELECT
		[Id],
		[FullName],
		[Organisation],
		[IsProfileEditor],
		[IsPolicyProfileUser],
		[UserName],
		[SsoUserId],
		[IsUserManagementSystem],
		[SsoUserIdExt],
		[SsoUserIdInt],
		[EmailAddress]
	FROM
		[User]
	WHERE
		[Id] = @UserId

	SELECT
		[ProfileId],
		[ProfileUserRoleId],
		[Name],
		[IsContributor]
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
		[ProfileSectionUser].[UserId] = @UserId

END
