using CDC.Web.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

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

    private static PlaywrightPdfService CreateService() =>
        new(new ConfigurationBuilder().Build(), NullLogger<PlaywrightPdfService>.Instance);
}
