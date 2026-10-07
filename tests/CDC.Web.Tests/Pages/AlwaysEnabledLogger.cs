using Microsoft.Extensions.Logging;

namespace CDC.Web.Tests.Pages;

// NullLogger.IsEnabled always returns false, which skips the body of every source-generated
// LoggerMessage method. This double returns true, so those bodies actually execute in tests.
// It also formats the message and reads back the state, which exercises the generated state
// struct in the same way a real logging provider would.
internal sealed class AlwaysEnabledLogger<T> : ILogger<T>
{
    private readonly List<string> messages = [];

    /// <summary>Gets every message logged so far, rendered as a real provider would render it.</summary>
    public IReadOnlyList<string> Messages => messages;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (state is IReadOnlyList<KeyValuePair<string, object?>> tags)
        {
            for (var index = 0; index < tags.Count; index++)
            {
                _ = tags[index];
            }
        }

        messages.Add(formatter(state, exception));
    }
}
