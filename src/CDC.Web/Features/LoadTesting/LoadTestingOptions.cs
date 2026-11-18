namespace CDC.Web.Features.LoadTesting;

/// <summary>
/// Configuration bound from the <c>LoadTesting</c> section. While <see cref="Enabled"/> is set, signing in as an internal or external
/// user skips the identity provider: the Load test login page asks for the email address of the user to sign in as, so a load test
/// needs no Entra ID or CIDM login. <see cref="LoadTestingOptionsValidator"/> decides where it may be switched on.
/// </summary>
public sealed class LoadTestingOptions
{
    /// <summary>The configuration section name this type binds to ("LoadTesting").</summary>
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the bypass is switched on. Off unless configured.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets when the bypass stops by itself. Required in a deployed environment, and at most a week ahead.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Reads only the <see cref="Enabled"/> switch, for the startup code that runs before the options are bound.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true"/> only when the configuration gives the switch a value of true; it is off when the value is missing or blank.</returns>
    public static bool IsEnabledIn(IConfiguration configuration)
    {
        var key = $"{SectionName}:{nameof(Enabled)}";

        // An environment whose task definition has no value for the flag leaves it out or passes it blank, and that must not stop the app starting.
        return !string.IsNullOrWhiteSpace(configuration[key]) && configuration.GetValue<bool>(key);
    }
}
