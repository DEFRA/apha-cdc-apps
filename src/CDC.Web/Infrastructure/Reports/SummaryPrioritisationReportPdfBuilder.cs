using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Builds the summary prioritisation report: reads the profile version's content and prioritisation
/// scores from CDC.Api, lays them out, renders the <c>SummaryPrioritisationReport</c> Razor view and
/// prints it with Playwright.
/// </summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
public sealed class SummaryPrioritisationReportPdfBuilder(
    IProfileReportsApiService reportsApi,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService) : IProfileReportPdfBuilder
{
    /// <summary>The Razor view that prints the summary prioritisation report.</summary>
    public const string ViewPath = "/Views/Pdf/SummaryPrioritisationReport.cshtml";

    /// <inheritdoc />
    public string ReportName => ProfilePrioritisationDocuments.ReportName;

    /// <inheritdoc />
    public async Task<byte[]> BuildAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var contentResult = await reportsApi.GetContentAsync(profileVersionId, cancellationToken);
        var scoresResult = await reportsApi.GetPrioritisationScoresAsync(profileVersionId, cancellationToken);

        if (contentResult.Outcome != ProfileReportsOutcome.Success || contentResult.Content is not { } content
            || scoresResult.Outcome != ProfileReportsOutcome.Success || scoresResult.Scores is not { } scores)
        {
            throw new InvalidOperationException(
                $"The summary prioritisation report of profile version '{profileVersionId}' could not be built: content {contentResult.Outcome}, scores {scoresResult.Outcome}.");
        }

        var document = ProfilePrioritisationDocuments.Build(content, scores);
        var html = await viewRenderer.RenderAsync(ViewPath, document);

        return await pdfService.RenderAsync(html, ProfilePrioritisationDocuments.BuildPdfOptions(document), cancellationToken);
    }
}
