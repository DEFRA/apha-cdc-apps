using System.Globalization;

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

    /// <summary>
    /// The configuration key of the hard stop (environment variable <c>LoadTesting__CutoffDate</c>): an ISO 8601 date, which still allows
    /// the whole of that day (UTC), or a date and time. From then on LoadTesting is off, whatever <see cref="Enabled"/> says.
    /// </summary>
    public const string CutoffDateKey = $"{SectionName}:CutoffDate";

    private static readonly string[] CutoffDateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm"
    ];

    /// <summary>Gets or sets a value indicating whether the bypass is switched on. Off unless configured, and off once the cutoff has passed.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets when the bypass stops by itself. Required in a deployed environment, and at most a week ahead.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets the moment the hard stop takes effect, read from <see cref="CutoffDateKey"/>; no cutoff when not set.</summary>
    public DateTimeOffset? CutoffUtc { get; set; }

    /// <summary>Reads whether LoadTesting is on, for the startup code that runs before the options are bound.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="timeProvider">The source of the current time; the system clock when not given.</param>
    /// <returns><see langword="true"/> only when the switch is given a value of true and the cutoff, if any, has not passed.</returns>
    public static bool IsEnabledIn(IConfiguration configuration, TimeProvider? timeProvider = null) =>
        IsSwitchedOnIn(configuration) && !IsPastCutoff(configuration, timeProvider ?? TimeProvider.System);

    /// <summary>Reads only the <see cref="Enabled"/> switch, ignoring the cutoff.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true"/> only when the configuration gives the switch a value of true; it is off when the value is missing or blank.</returns>
    public static bool IsSwitchedOnIn(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var key = $"{SectionName}:{nameof(Enabled)}";

        // An environment whose task definition has no value for the flag leaves it out or passes it blank, and that must not stop the app starting.
        return !string.IsNullOrWhiteSpace(configuration[key]) && configuration.GetValue<bool>(key);
    }

    /// <summary>Reads the hard stop from <see cref="CutoffDateKey"/>.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="cutoffUtc">The moment LoadTesting stops, or <see langword="null"/> when no cutoff is set.</param>
    /// <returns><see langword="false"/> when a value is set but is not an ISO 8601 date or date and time.</returns>
    public static bool TryReadCutoff(IConfiguration configuration, out DateTimeOffset? cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        cutoffUtc = null;
        var value = configuration[CutoffDateKey]?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            // "After that date": the whole cutoff day still counts, so it stops at the following midnight UTC.
            cutoffUtc = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            return true;
        }

        if (DateTimeOffset.TryParseExact(value, CutoffDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
        {
            cutoffUtc = moment;

            return true;
        }

        return false;
    }

    /// <summary>Gets whether the hard stop has been reached. A value that is not a date counts as reached, so a mistake keeps LoadTesting off.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="timeProvider">The source of the current time.</param>
    /// <returns><see langword="true"/> when LoadTesting must be off because of the cutoff.</returns>
    public static bool IsPastCutoff(IConfiguration configuration, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return !TryReadCutoff(configuration, out var cutoffUtc) || (cutoffUtc is { } cutoff && timeProvider.GetUtcNow() >= cutoff);
    }
}
