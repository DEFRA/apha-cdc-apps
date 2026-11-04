using Microsoft.Extensions.Options;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Tells the rest of the app whether sign-in is being bypassed for load testing.</summary>
public interface ILoadTestingStatus
{
    /// <summary>Gets a value indicating whether LoadTesting is switched on in configuration.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets a value indicating whether sign-in is bypassed right now: switched on, and not past <see cref="ExpiresAtUtc"/>.</summary>
    bool IsActive { get; }

    /// <summary>Gets when the bypass stops by itself, if it is set to.</summary>
    DateTimeOffset? ExpiresAtUtc { get; }
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
    public bool IsActive => IsEnabled && (ExpiresAtUtc is not { } expiresAtUtc || timeProvider.GetUtcNow() < expiresAtUtc);
}
