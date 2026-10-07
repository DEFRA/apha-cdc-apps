namespace CDC.Api.Features.ProfileContributors;

/// <summary>
/// Source-generated structured log messages for the profile contributors feature. Nothing here
/// records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class ProfileContributorsLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Retrieved {ContributorCount} contributors for profile {ProfileId}")]
    public static partial void RetrievedProfileContributors(this ILogger logger, int contributorCount, Guid profileId);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Information, Message = "Retrieved editable detail for contributor {ContributorId} on profile {ProfileId}")]
    public static partial void RetrievedContributorForEdit(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Information, Message = "No contributor {ContributorId} found on profile {ProfileId}")]
    public static partial void ContributorForEditNotFound(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Information, Message = "Retrieved {RoleCount} contributor roles")]
    public static partial void RetrievedProfileUserRoles(this ILogger logger, int roleCount);

    [LoggerMessage(EventId = 1305, Level = LogLevel.Information, Message = "Updating contributor {ContributorId} on profile {ProfileId}")]
    public static partial void UpdatingContributor(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1306, Level = LogLevel.Information, Message = "Updated contributor {ContributorId} on profile {ProfileId}")]
    public static partial void UpdatedContributor(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1307, Level = LogLevel.Warning, Message = "Rejected a concurrent edit to contributor {ContributorId}")]
    public static partial void ContributorConcurrencyConflict(this ILogger logger, Guid contributorId);

    [LoggerMessage(EventId = 1308, Level = LogLevel.Information, Message = "Username lookup matched existing global user {UserId}")]
    public static partial void UsernameMatchedExistingUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1309, Level = LogLevel.Information, Message = "Username lookup found a validly-formatted new username")]
    public static partial void UsernameIsNew(this ILogger logger);

    [LoggerMessage(EventId = 1310, Level = LogLevel.Information, Message = "Username lookup rejected an invalidly-formatted username")]
    public static partial void UsernameInvalidFormat(this ILogger logger);

    [LoggerMessage(EventId = 1311, Level = LogLevel.Information, Message = "Username lookup blocked a user management system account")]
    public static partial void UsernameBlocked(this ILogger logger);

    [LoggerMessage(EventId = 1312, Level = LogLevel.Information, Message = "Adding contributor {ContributorId} to profile {ProfileId}")]
    public static partial void AddingContributor(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1313, Level = LogLevel.Information, Message = "Added contributor {ContributorId} to profile {ProfileId}")]
    public static partial void AddedContributor(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1314, Level = LogLevel.Warning, Message = "Rejected a duplicate username adding contributor {ContributorId}")]
    public static partial void DuplicateUsernameConflict(this ILogger logger, Guid contributorId);

    [LoggerMessage(EventId = 1315, Level = LogLevel.Information, Message = "Deleting contributor {ContributorId} from profile {ProfileId}")]
    public static partial void DeletingContributor(this ILogger logger, Guid contributorId, Guid profileId);

    [LoggerMessage(EventId = 1316, Level = LogLevel.Information, Message = "Deleted contributor {ContributorId} from profile {ProfileId}")]
    public static partial void DeletedContributor(this ILogger logger, Guid contributorId, Guid profileId);
}
