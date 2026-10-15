using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Builds a profile report PDF: reads the profile version's content from CDC.Api, lays it out,
/// renders the <c>ProfileReportDocument</c> Razor view to HTML and prints it with Playwright.
/// </summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
public abstract class ProfileReportDocumentPdfBuilder(
    IProfileReportsApiService reportsApi,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService) : IProfileReportPdfBuilder
{
    /// <summary>The Razor view that prints a profile report document.</summary>
    public const string ViewPath = "/Views/Pdf/ProfileReportDocument.cshtml";

    /// <inheritdoc />
    public abstract string ReportName { get; }

    /// <inheritdoc />
    public async Task<byte[]> BuildAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var content = await LoadContentAsync(profileVersionId, cancellationToken);
        var document = await BuildDocumentAsync(content, cancellationToken);

        return await RenderAsync(document, cancellationToken);
    }

    /// <summary>Reads the content the report prints.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The content.</returns>
    /// <exception cref="InvalidOperationException">The content could not be read.</exception>
    protected async Task<ProfileReportContentDto> LoadContentAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await reportsApi.GetContentAsync(profileVersionId, cancellationToken);

        return result.Outcome == ProfileReportsOutcome.Success && result.Content is { } content
            ? content
            : throw new InvalidOperationException($"The content of profile version '{profileVersionId}' could not be read: {result.Outcome}.");
    }

    /// <summary>Renders a laid-out document to PDF.</summary>
    /// <param name="document">The document to print.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The PDF.</returns>
    protected async Task<byte[]> RenderAsync(ProfileReportDocumentViewModel document, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync(ViewPath, document);

        return await pdfService.RenderAsync(html, ProfileReportLayout.BuildPdfOptions(document), cancellationToken);
    }

    /// <summary>Lays the content out for this report.</summary>
    /// <param name="content">The profile version's content.</param>
    /// <param name="cancellationToken">Cancels any further reads the layout needs.</param>
    /// <returns>The document.</returns>
    protected abstract Task<ProfileReportDocumentViewModel> BuildDocumentAsync(ProfileReportContentDto content, CancellationToken cancellationToken);
}

/// <summary>Builds the summary profile report.</summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
public sealed class SummaryProfileReportPdfBuilder(IProfileReportsApiService reportsApi, IRazorViewRenderer viewRenderer, IPlaywrightPdfService pdfService)
    : ProfileReportDocumentPdfBuilder(reportsApi, viewRenderer, pdfService)
{
    /// <inheritdoc />
    public override string ReportName => ProfileReportDocuments.SummaryProfileReportName;

    /// <inheritdoc />
    protected override Task<ProfileReportDocumentViewModel> BuildDocumentAsync(ProfileReportContentDto content, CancellationToken cancellationToken) =>
        Task.FromResult(ProfileReportDocuments.BuildSummaryProfile(content));
}

/// <summary>Builds the question and answer brief.</summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
public sealed class QABriefReportPdfBuilder(IProfileReportsApiService reportsApi, IRazorViewRenderer viewRenderer, IPlaywrightPdfService pdfService)
    : ProfileReportDocumentPdfBuilder(reportsApi, viewRenderer, pdfService)
{
    /// <inheritdoc />
    public override string ReportName => ProfileReportDocuments.QABriefReportName;

    /// <inheritdoc />
    protected override Task<ProfileReportDocumentViewModel> BuildDocumentAsync(ProfileReportContentDto content, CancellationToken cancellationToken) =>
        Task.FromResult(ProfileReportDocuments.BuildQABrief(content));
}

/// <summary>Builds the full profile report, and the single-section print of it.</summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
/// <param name="referenceLoader">Loads the references printed on the landscape pages.</param>
public sealed class FullProfileReportPdfBuilder(
    IProfileReportsApiService reportsApi,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService,
    ProfileReportReferenceLoader referenceLoader)
    : ProfileReportDocumentPdfBuilder(reportsApi, viewRenderer, pdfService)
{
    /// <inheritdoc />
    public override string ReportName => ProfileReportDocuments.FullProfileReportName;

    /// <summary>Builds the print of one section, with no front page and that section's references.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="profileSectionId">The section to print.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The PDF.</returns>
    public async Task<byte[]> BuildSectionAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken)
    {
        var content = await LoadContentAsync(profileVersionId, cancellationToken);
        var references = await referenceLoader.LoadAsync(content, profileSectionId, cancellationToken);

        return await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, profileSectionId, references), cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<ProfileReportDocumentViewModel> BuildDocumentAsync(ProfileReportContentDto content, CancellationToken cancellationToken) =>
        ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, await referenceLoader.LoadAsync(content, Guid.Empty, cancellationToken));
}
