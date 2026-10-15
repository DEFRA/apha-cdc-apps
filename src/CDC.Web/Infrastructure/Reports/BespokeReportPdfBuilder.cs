using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Builds the bespoke report: reads the profile version's content and the ticked guidance from
/// CDC.Api, lays out the ticked sections, renders the <c>ProfileReportDocument</c> Razor view to
/// HTML and prints it with Playwright.
/// </summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
public sealed class BespokeReportPdfBuilder(
    IProfileReportsApiService reportsApi,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService) : IBespokeReportPdfBuilder
{
    /// <inheritdoc />
    public async Task<byte[]> BuildAsync(Guid profileVersionId, BespokeReportSelection selection, string? templateTitle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var contentResult = await reportsApi.GetContentAsync(profileVersionId, cancellationToken);

        if (contentResult.Outcome != ProfileReportsOutcome.Success || contentResult.Content is not { } content)
        {
            throw new InvalidOperationException($"The content of profile version '{profileVersionId}' could not be read: {contentResult.Outcome}.");
        }

        var guidance = await LoadGuidanceAsync(ProfileBespokeReportDocuments.GuidanceQuestionIds(content, selection), cancellationToken);
        var document = ProfileBespokeReportDocuments.Build(content, selection, guidance, templateTitle);
        var html = await viewRenderer.RenderAsync(ProfileReportDocumentPdfBuilder.ViewPath, document);

        return await pdfService.RenderAsync(html, ProfileReportLayout.BuildPdfOptions(document), cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        if (questionIds.Count == 0)
        {
            return [];
        }

        var result = await reportsApi.GetQuestionGuidanceAsync(questionIds, cancellationToken);

        return result.Outcome == ProfileReportsOutcome.Success && result.Guidance is { } guidance
            ? guidance.ToDictionary(item => item.QuestionId, item => item.UserGuidance)
            : throw new InvalidOperationException($"The guidance of the selected questions could not be read: {result.Outcome}.");
    }
}
