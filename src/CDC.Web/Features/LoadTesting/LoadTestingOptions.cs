namespace CDC.Web.Features.LoadTesting;

/// <summary>
/// Configuration bound from the <c>LoadTesting</c> section. While <see cref="Enabled"/> is set, signing in as an internal or external
/// user skips the identity provider and signs in as the configured user, so a load test needs no Entra ID or CIDM login.
/// <see cref="LoadTestingOptionsValidator"/> decides where it may be switched on.
/// </summary>
public sealed class LoadTestingOptions
{
    /// <summary>The configuration section name this type binds to ("LoadTesting").</summary>
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the bypass is switched on. Off unless configured.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets when the bypass stops by itself. Required in a deployed environment, and at most a week ahead.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets the <c>[dbo].[User].UserName</c> of the internal user to sign in as.</summary>
    public string InternalUserName { get; set; } = string.Empty;

    /// <summary>Gets or sets that internal user's <c>SsoUserIdInt</c> (Entra ID object id), so CDC.Api matches the existing row and leaves it unchanged.</summary>
    public Guid InternalSsoUserId { get; set; }

    /// <summary>Gets or sets the email address of the pre-registered external user to sign in as.</summary>
    public string ExternalUserEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets that external user's <c>SsoUserIdExt</c> (CIDM subject id), so CDC.Api matches the existing row and leaves it unchanged.</summary>
    public Guid ExternalSsoUserId { get; set; }

    /// <summary>Reads only the <see cref="Enabled"/> switch, for the startup code that runs before the options are bound.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true"/> when the bypass is switched on.</returns>
    public static bool IsEnabledIn(IConfiguration configuration) =>
        configuration.GetValue<bool>($"{SectionName}:{nameof(Enabled)}");
}
