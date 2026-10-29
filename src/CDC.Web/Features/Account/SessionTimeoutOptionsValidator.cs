using Microsoft.Extensions.Options;

namespace CDC.Web.Features.Account;

/// <summary>Fails fast at startup when <see cref="SessionTimeoutOptions"/> would end a session without a user being able to respond to the warning.</summary>
public sealed class SessionTimeoutOptionsValidator : IValidateOptions<SessionTimeoutOptions>
{
    // WCAG 2.2 success criterion 2.2.1 (Timing Adjustable): a user must have at least 20 seconds to extend a time limit.
    private static readonly TimeSpan MinimumWarning = TimeSpan.FromSeconds(20);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SessionTimeoutOptions options)
    {
        var failures = new List<string>();

        if (options.WarningBeforeExpiry < MinimumWarning)
        {
            failures.Add(
                $"{nameof(SessionTimeoutOptions)}.{nameof(SessionTimeoutOptions.WarningBeforeExpiry)} must be at least " +
                $"{MinimumWarning.TotalSeconds} seconds, so that a user has time to respond to it.");
        }

        if (options.IdleTimeout <= options.WarningBeforeExpiry)
        {
            failures.Add(
                $"{nameof(SessionTimeoutOptions)}.{nameof(SessionTimeoutOptions.IdleTimeout)} must be longer than " +
                $"{nameof(SessionTimeoutOptions.WarningBeforeExpiry)}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
