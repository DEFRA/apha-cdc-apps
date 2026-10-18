using CDC.Common.Contracts;
using CDC.Web.Infrastructure;

namespace CDC.Web.Features.DiseaseRanking;

/// <summary>A generated report document.</summary>
/// <param name="FileName">The suggested download file name, including the extension.</param>
/// <param name="Content">The PDF bytes.</param>
public sealed record GeneratedPdf(string FileName, byte[] Content);

/// <summary>Produces the disease ranking report PDFs.</summary>
public interface IDiseaseRankingPdfService
{
    /// <summary>Builds the PDF for one report.</summary>
    /// <param name="reportKey">The key from <c>GET /api/disease-ranking/report-options</c>.</param>
    /// <param name="kind">Scored or ranked graphs.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF, or <see langword="null"/> when no such report exists.</returns>
    Task<GeneratedPdf?> GenerateAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IDiseaseRankingPdfService"/>: reads the computed report from CDC.Api, renders
/// it through a Razor view to HTML, then converts the HTML to PDF.
/// </summary>
/// <param name="apiService">Reads the report data.</param>
/// <param name="viewRenderer">Renders the report view.</param>
/// <param name="pdfRenderer">Converts the HTML to PDF.</param>
/// <param name="timeProvider">Supplies the generation date shown in the footer.</param>
public sealed class DiseaseRankingPdfService(
    IDiseaseRankingApiService apiService,
    IRazorViewToStringRenderer viewRenderer,
    IPdfRenderer pdfRenderer,
    TimeProvider timeProvider) : IDiseaseRankingPdfService
{
    /// <summary>The report view's application-relative path.</summary>
    public const string ViewPath = "~/Features/DiseaseRanking/Views/DiseaseRankingReport.cshtml";

    /// <inheritdoc />
    public async Task<GeneratedPdf?> GenerateAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken)
    {
        var report = await apiService.GetReportAsync(reportKey, kind, cancellationToken);

        if (report is null)
        {
            return null;
        }

        var html = await viewRenderer.RenderAsync(ViewPath, report);
        var generatedOn = timeProvider.GetLocalNow().ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var content = await pdfRenderer.RenderAsync(html, $"{report.FooterTitle} Generated: {generatedOn}.", cancellationToken);

        return new GeneratedPdf($"{report.FileName}.pdf", content);
    }
}
