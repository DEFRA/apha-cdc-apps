namespace CDC.Web.Features.Account;

/// <summary>Configuration bound from the <c>SessionTimeout</c> section, controlling how long a signed-in session may sit idle.</summary>
public sealed class SessionTimeoutOptions
{
    /// <summary>The configuration section name this type binds to ("SessionTimeout").</summary>
    public const string SectionName = "SessionTimeout";

    /// <summary>
    /// How long a signed-in session may sit idle - with no request to the app - before the user has to sign in again. It is the
    /// lifetime of both sign-in cookies. The default is the 20 minutes the legacy application's session lasted.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// How long before the session ends the user is asked whether to extend it. The default is the 5 minutes the legacy
    /// application asked ahead.
    /// </summary>
    public TimeSpan WarningBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);
}
