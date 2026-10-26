using System.Reflection;
using Microsoft.Playwright;

namespace CDC.Web.Tests.TestSupport;

/// <summary>Implements a Playwright interface at runtime, answering each member by name, so a test needs only the members it uses.</summary>
public class PlaywrightFake : DispatchProxy
{
    private Func<string, object?> answer = name => throw new NotSupportedException(name);

    public static T Create<T>(Func<string, object?> answer)
        where T : class
    {
        var fake = DispatchProxy.Create<T, PlaywrightFake>();
        ((PlaywrightFake)(object)fake).answer = answer;

        return fake;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => answer(targetMethod!.Name);
}

/// <summary>A stand-in for a Chromium browser that prints <c>pdf</c> for every page, and can be made to exit or fail.</summary>
internal sealed class FakeChromium
{
    public FakeChromium(byte[] pdf)
    {
        var page = PlaywrightFake.Create<IPage>(name => name switch
        {
            nameof(IPage.SetContentAsync) => Task.CompletedTask,
            nameof(IPage.PdfAsync) => Task.FromResult(pdf),
            _ => throw new NotSupportedException(name)
        });
        var context = PlaywrightFake.Create<IBrowserContext>(name => name switch
        {
            nameof(IBrowserContext.NewPageAsync) => Task.FromResult(page),
            nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
            _ => throw new NotSupportedException(name)
        });

        Browser = PlaywrightFake.Create<IBrowser>(name => name switch
        {
            "get_IsConnected" => IsConnected,
            nameof(IBrowser.NewContextAsync) => ThrowOnNewContext is null
                ? Task.FromResult(context)
                : Task.FromException<IBrowserContext>(ThrowOnNewContext),
            nameof(IAsyncDisposable.DisposeAsync) => new ValueTask(CloseAsync()),
            _ => throw new NotSupportedException(name)
        });
    }

    public IBrowser Browser { get; }

    public bool IsConnected { get; set; } = true;

    public Exception? ThrowOnNewContext { get; set; }

    public Exception? ThrowOnClose { get; set; }

    public bool IsClosed { get; private set; }

    private Task CloseAsync()
    {
        IsClosed = true;

        return ThrowOnClose is null ? Task.CompletedTask : Task.FromException(ThrowOnClose);
    }
}
