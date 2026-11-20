using Microsoft.Extensions.Options;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Tells the rest of the app whether sign-in is being bypassed for load testing.</summary>
public interface ILoadTestingStatus
{
    /// <summary>Gets a value indicating whether LoadTesting is switched on in configuration (and its cutoff had not passed at startup).</summary>
    bool IsEnabled { get; }

    /// <summary>Gets a value indicating whether sign-in is bypassed right now: switched on, and past neither <see cref="ExpiresAtUtc"/> nor <see cref="CutoffUtc"/>.</summary>
    bool IsActive { get; }

    /// <summary>Gets when the bypass stops by itself, if it is set to.</summary>
    DateTimeOffset? ExpiresAtUtc { get; }

    /// <summary>Gets the hard stop, after which LoadTesting is off whatever else is configured.</summary>
    DateTimeOffset? CutoffUtc { get; }

    /// <summary>Gets the earlier of <see cref="ExpiresAtUtc"/> and <see cref="CutoffUtc"/>: when the bypass next switches off.</summary>
    DateTimeOffset? SwitchesOffAtUtc { get; }
}

/// <summary>Default <see cref="ILoadTestingStatus"/>, read from <see cref="LoadTestingOptions"/>.</summary>
/// <param name="options">The LoadTesting configuration.</param>
/// <param name="timeProvider">The source of the current time.</param>
public sealed class LoadTestingStatus(IOptions<LoadTestingOptions> options, TimeProvider timeProvider) : ILoadTestingStatus
{
    /// <inheritdoc />
    public bool IsEnabled => options.Value.Enabled;

    /// <inheritdoc />
    public DateTimeOffset? ExpiresAtUtc => options.Value.ExpiresAtUtc;

    /// <inheritdoc />
    public DateTimeOffset? CutoffUtc => options.Value.CutoffUtc;

    /// <inheritdoc />
    public DateTimeOffset? SwitchesOffAtUtc => (ExpiresAtUtc, CutoffUtc) switch
    {
        ({ } expires, { } cutoff) => expires < cutoff ? expires : cutoff,
        (var expires, var cutoff) => expires ?? cutoff
    };

    /// <inheritdoc />
    public bool IsActive => IsEnabled && (SwitchesOffAtUtc is not { } switchesOffAtUtc || timeProvider.GetUtcNow() < switchesOffAtUtc);
}
