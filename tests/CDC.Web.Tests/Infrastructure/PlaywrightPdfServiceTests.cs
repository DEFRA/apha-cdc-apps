using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace CDC.Web.Tests.Infrastructure;

public class PlaywrightPdfServiceTests
{
    [Fact]
    public async Task RenderAsync_RejectsBlankHtml()
    {
        await using var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RenderAsync(" ", new PdfRenderOptions()));
    }

    [Fact]
    public async Task RenderAsync_RejectsNullOptions()
    {
        await using var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RenderAsync("<html/>", null!));
    }

    // Chromium may or may not be installed where the tests run: with it the result must be a real PDF,
    // without it the failure must surface as a PdfRenderException rather than a raw Playwright error.
    [Fact]
    public async Task RenderAsync_ReturnsAPdfOrAPdfRenderException()
    {
        await using var service = CreateService();
        var options = new PdfRenderOptions { HeaderHtml = "<div>Header</div>", FooterHtml = "<div>Footer</div>" };

        try
        {
            var pdf = await service.RenderAsync("<html><body><p>Hello</p></body></html>", options);

            Assert.Equal("%PDF"u8.ToArray(), pdf.Take(4).ToArray());
        }
        catch (PdfRenderException exception)
        {
            Assert.NotNull(exception.Message);
        }
    }

    [Fact]
    public async Task DisposeAsync_CompletesWhenNoBrowserWasStarted()
    {
        var service = CreateService();

        await service.DisposeAsync();
    }

    [Fact]
    public void PdfRenderException_SupportsEveryConstructor()
    {
        var inner = new InvalidOperationException("inner");

        Assert.NotNull(new PdfRenderException().Message);
        Assert.Equal("message", new PdfRenderException("message").Message);
        Assert.Same(inner, new PdfRenderException("message", inner).InnerException);
    }

    [Fact]
    public async Task RenderAsync_StartsTheBrowserOnce_AndReusesItWhileItIsRunning()
    {
        var chromium = new FakeChromium(Pdf);
        var launches = 0;
        await using var service = CreateService(() => { launches++; return chromium; });

        Assert.Equal(Pdf, await service.RenderAsync(Html, new PdfRenderOptions()));
        Assert.Equal(Pdf, await service.RenderAsync(Html, new PdfRenderOptions()));

        Assert.Equal(1, launches);
    }

    // Once Chromium had exited, every later report failed until the task restarted.
    [Fact]
    public async Task RenderAsync_StartsANewBrowser_WhenTheLastOneHasExited()
    {
        var first = new FakeChromium(Pdf);
        var second = new FakeChromium(Pdf);
        var browsers = new Queue<FakeChromium>([first, second]);
        await using var service = CreateService(browsers.Dequeue);
        await service.RenderAsync(Html, new PdfRenderOptions());
        first.IsConnected = false;

        var pdf = await service.RenderAsync(Html, new PdfRenderOptions());

        Assert.Equal(Pdf, pdf);
        Assert.Empty(browsers);
        Assert.True(first.IsClosed);
        Assert.False(second.IsClosed);
    }

    [Fact]
    public async Task RenderAsync_StartsANewBrowser_EvenWhenTheExitedOneCannotBeClosed()
    {
        var first = new FakeChromium(Pdf) { ThrowOnClose = new PlaywrightException("already gone") };
        var browsers = new Queue<FakeChromium>([first, new FakeChromium(Pdf)]);
        await using var service = CreateService(browsers.Dequeue);
        await service.RenderAsync(Html, new PdfRenderOptions());
        first.IsConnected = false;

        Assert.Equal(Pdf, await service.RenderAsync(Html, new PdfRenderOptions()));
        Assert.Empty(browsers);
    }

    // This is the failure seen in AWS: the browser still looked connected, but closed as soon as it was used.
    [Fact]
    public async Task RenderAsync_RetriesOnceWithANewBrowser_WhenTheBrowserClosesDuringTheRender()
    {
        var first = new FakeChromium(Pdf) { ThrowOnNewContext = new PlaywrightException("Target page, context or browser has been closed") };
        var browsers = new Queue<FakeChromium>([first, new FakeChromium(Pdf)]);
        await using var service = CreateService(browsers.Dequeue);

        var pdf = await service.RenderAsync(Html, new PdfRenderOptions());

        Assert.Equal(Pdf, pdf);
        Assert.Empty(browsers);
        Assert.True(first.IsClosed);
    }

    [Fact]
    public async Task RenderAsync_ThrowsAPdfRenderException_AndDoesNotRetryAgain_WhenTheNewBrowserClosesToo()
    {
        var browsers = new Queue<FakeChromium>(
        [
            new FakeChromium(Pdf) { ThrowOnNewContext = new PlaywrightException("Target page, context or browser has been closed") },
            new FakeChromium(Pdf) { ThrowOnNewContext = new PlaywrightException("Target page, context or browser has been closed") },
            new FakeChromium(Pdf)
        ]);
        await using var service = CreateService(browsers.Dequeue);

        var exception = await Assert.ThrowsAsync<PdfRenderException>(() => service.RenderAsync(Html, new PdfRenderOptions()));

        Assert.IsType<PlaywrightException>(exception.InnerException);
        Assert.Single(browsers);
    }

    [Fact]
    public async Task RenderAsync_ThrowsAPdfRenderException_WhenTheBrowserCannotStart()
    {
        await using var service = CreateService(() => throw new PlaywrightException("Executable doesn't exist"));

        var exception = await Assert.ThrowsAsync<PdfRenderException>(() => service.RenderAsync(Html, new PdfRenderOptions()));

        Assert.IsType<PlaywrightException>(exception.InnerException);
    }

    [Fact]
    public async Task RenderAsync_ThrowsAPdfRenderException_WhenTheBrowserReturnsSomethingOtherThanAPdf()
    {
        await using var service = CreateService(() => new FakeChromium("<html>"u8.ToArray()));

        var exception = await Assert.ThrowsAsync<PdfRenderException>(() => service.RenderAsync(Html, new PdfRenderOptions()));

        Assert.Equal("The browser returned data that is not a PDF.", exception.Message);
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheBrowser()
    {
        var chromium = new FakeChromium(Pdf);
        var service = CreateService(() => chromium);
        await service.RenderAsync(Html, new PdfRenderOptions());

        await service.DisposeAsync();

        Assert.True(chromium.IsClosed);
    }

    private const string Html = "<html><body><p>Hello</p></body></html>";

    private static readonly byte[] Pdf = "%PDF-1.7"u8.ToArray();

    private static PlaywrightPdfService CreateService() =>
        new(new ConfigurationBuilder().Build(), NullLogger<PlaywrightPdfService>.Instance);

    private static PlaywrightPdfService CreateService(Func<FakeChromium> launch) =>
        new(new ConfigurationBuilder().Build(), NullLogger<PlaywrightPdfService>.Instance, _ => Task.FromResult(launch().Browser));
}
