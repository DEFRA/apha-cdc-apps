using Microsoft.Extensions.Logging;

namespace CDC.Web.Tests.Pages;

// NullLogger.IsEnabled always returns false, which skips the body of every source-generated
// LoggerMessage method. This double returns true, so those bodies actually execute in tests.
internal sealed class AlwaysEnabledLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
