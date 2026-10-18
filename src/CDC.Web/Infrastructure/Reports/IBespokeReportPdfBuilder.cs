using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>Builds the bespoke report PDF for the sections, questions and guidance a user ticked.</summary>
public interface IBespokeReportPdfBuilder
{
    /// <summary>Builds the report.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="selection">What was ticked on the tree.</param>
    /// <param name="templateTitle">The selected template's title, or <see langword="null"/> when none is selected.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The PDF.</returns>
    Task<byte[]> BuildAsync(Guid profileVersionId, BespokeReportSelection selection, string? templateTitle, CancellationToken cancellationToken);
}
