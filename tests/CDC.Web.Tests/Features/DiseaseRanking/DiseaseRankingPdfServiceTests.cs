using CDC.Common.Contracts;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Features.DiseaseRanking;

public class DiseaseRankingPdfServiceTests
{
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46];

    private readonly FakeApi api = new();
    private readonly FakeViews views = new();
    private readonly FakePdf pdf = new();

    private DiseaseRankingPdfService CreateService() => new(api, views, pdf, new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task GenerateAsync_RendersTheReportThroughTheViewAndPdfRenderer()
    {
        api.Report = new DiseaseRankingReportDto { Title = "Title", FooterTitle = "RFI scoring report for Terrestrial.", FileName = "DiseaseRankingReport_Scored_Terrestrial" };

        var generated = await CreateService().GenerateAsync("terrestrial", DiseaseRankingReportKind.Scored, CancellationToken.None);

        Assert.NotNull(generated);
        Assert.Equal("DiseaseRankingReport_Scored_Terrestrial.pdf", generated.FileName);
        Assert.Equal(PdfBytes, generated.Content);
        Assert.Equal(DiseaseRankingPdfService.ViewPath, views.LastViewPath);
        Assert.Same(api.Report, views.LastModel);
        Assert.Equal("<html>rendered</html>", pdf.LastHtml);
        Assert.Equal("RFI scoring report for Terrestrial. Generated: 13/10/2026.", pdf.LastFooter);
        Assert.Equal(("terrestrial", DiseaseRankingReportKind.Scored), api.LastRequest);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_AndDoesNotRender_WhenTheReportDoesNotExist()
    {
        api.Report = null;

        var generated = await CreateService().GenerateAsync("bogus", DiseaseRankingReportKind.Ranked, CancellationToken.None);

        Assert.Null(generated);
        Assert.Null(views.LastViewPath);
        Assert.Null(pdf.LastHtml);
    }

    private sealed class FakeApi : IDiseaseRankingApiService
    {
        public DiseaseRankingReportDto? Report { get; set; }

        public (string Key, DiseaseRankingReportKind Kind)? LastRequest { get; private set; }

        public Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DiseaseRankingReportOptionDto>>([]);

        public Task<DiseaseRankingReportDto?> GetReportAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken = default)
        {
            LastRequest = (reportKey, kind);

            return Task.FromResult(Report);
        }
    }

    private sealed class FakeViews : IRazorViewToStringRenderer
    {
        public string? LastViewPath { get; private set; }

        public object? LastModel { get; private set; }

        public Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            LastViewPath = viewPath;
            LastModel = model;

            return Task.FromResult("<html>rendered</html>");
        }
    }

    private sealed class FakePdf : IPdfRenderer
    {
        public string? LastHtml { get; private set; }

        public string? LastFooter { get; private set; }

        public Task<byte[]> RenderAsync(string html, string footerText, CancellationToken cancellationToken)
        {
            LastHtml = html;
            LastFooter = footerText;

            return Task.FromResult(PdfBytes);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
