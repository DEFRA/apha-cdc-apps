namespace CDC.Web.Features.LoadTesting;

/// <summary>Source-generated log messages for LoadTesting. Only opaque ids are logged - never a user name or email address.</summary>
internal static partial class LoadTestingLog
{
    [LoggerMessage(EventId = 9500, Level = LogLevel.Warning, Message = "LoadTesting is ENABLED: sign-in skips the identity provider (deployment {Deployment}, expires {ExpiresAtUtc})")]
    public static partial void Enabled(this ILogger logger, string deployment, DateTimeOffset? expiresAtUtc);

    [LoggerMessage(EventId = 9501, Level = LogLevel.Information, Message = "LoadTesting signed in the {Kind} user {UserId} without the identity provider")]
    public static partial void SignedIn(this ILogger logger, string kind, Guid userId);

    [LoggerMessage(EventId = 9502, Level = LogLevel.Warning, Message = "LoadTesting could not sign in the {Kind} user: {Outcome}")]
    public static partial void SignInRefused(this ILogger logger, string kind, LoadTestSignInOutcome outcome);

    [LoggerMessage(EventId = 9503, Level = LogLevel.Information, Message = "A LoadTesting session was ended because the LoadTesting window has passed")]
    public static partial void SessionEnded(this ILogger logger);

    [LoggerMessage(EventId = 9504, Level = LogLevel.Error, Message = "LoadTesting could not look up the {Kind} user in CDC.Api")]
    public static partial void LookupFailed(this ILogger logger, string kind, Exception exception);
}
