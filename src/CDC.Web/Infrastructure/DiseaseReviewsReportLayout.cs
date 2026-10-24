using System.Globalization;
using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Lays out the disease reviews report data for printing, matching the legacy <c>DiseaseReviewsReportPdf</c>:
/// the description and a line stating whose reviews are due between which dates, then a table per profile
/// version listing the policy, author and VA review dates of its sections.
/// </summary>
public static class DiseaseReviewsReportLayout
{
    private const string RangeDateFormat = "dd-MM-yyyy";

    // The legacy footer asked for "dd-MM-YY", which prints the letters YY, so the two-digit year it meant is used instead.
    private const string FooterDateFormat = "dd-MM-yy";

    private const string DueDateFormat = "dd MMM yyyy";

    private const string ProducedDateFormat = "dd/MM/yyyy";

    // The legacy file name ended "_{Now}", which became "dd_MM_yyyy_HH_mm_ss" once the date and time separators were replaced.
    private const string FileNameTimestampFormat = "dd_MM_yyyy_HH_mm_ss";

    /// <summary>Builds the report's view model from the API data.</summary>
    /// <param name="report">The report data.</param>
    /// <param name="description">The description typed on the form.</param>
    /// <param name="authorName">The full name of the user whose reviews are due.</param>
    /// <param name="fromDate">The first day of the range.</param>
    /// <param name="toDate">The last day of the range.</param>
    /// <returns>The laid-out report.</returns>
    public static DiseaseReviewsReportViewModel Build(
        DiseaseReviewsReportDto report,
        string description,
        string authorName,
        DateTime fromDate,
        DateTime toDate)
    {
        ArgumentNullException.ThrowIfNull(report);

        // The legacy report grouped the rows by profile version in the order each first appeared, then numbered each profile's sections in order.
        return new DiseaseReviewsReportViewModel(
            description,
            $"{authorName} due between {Format(fromDate, RangeDateFormat)} and {Format(toDate, RangeDateFormat)}",
            [.. report.Sections
                .GroupBy(row => row.ProfileVersionId)
                .Select(group => new DiseaseReviewsReportProfileView(
                    BuildProfileName(group.First()),
                    [.. group.OrderBy(row => row.SectionNumber).Select(BuildSectionView)]))]);
    }

    /// <summary>Builds the page footer: whose reviews and which dates, then when the report was produced and the page number.</summary>
    /// <param name="authorName">The full name of the user whose reviews are due.</param>
    /// <param name="fromDate">The first day of the range.</param>
    /// <param name="toDate">The last day of the range.</param>
    /// <param name="producedOn">When the report was produced.</param>
    /// <returns>The render options.</returns>
    public static PdfRenderOptions BuildPdfOptions(string authorName, DateTime fromDate, DateTime toDate, DateTimeOffset producedOn) => new()
    {
        // border-box keeps the right-aligned line inside the page, rather than a margin's width past its edge.
        FooterHtml =
            "<div style=\"width:100%;box-sizing:border-box;padding:0 12.7mm;font-family:Arial,Helvetica,sans-serif;font-size:9px;\">" +
            $"<div style=\"text-align:left;\">{WebUtility.HtmlEncode(ProfileTitleHtmlFormatter.ToPlainText(authorName))} due {Format(fromDate, FooterDateFormat)}-{Format(toDate, FooterDateFormat)}</div>" +
            $"<div style=\"text-align:right;\">Produced: {producedOn.ToString(ProducedDateFormat, CultureInfo.InvariantCulture)}. " +
            "Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div></div>"
    };

    /// <summary>Builds the file name offered for download, matching the legacy "DiseaseReviewsReport_{date and time}".</summary>
    /// <param name="generatedAt">When the report was generated.</param>
    /// <returns>A file name ending ".pdf".</returns>
    public static string BuildFileName(DateTimeOffset generatedAt) =>
        PdfFileName.Build($"DiseaseReviewsReport_{generatedAt.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture)}");

    /// <summary>Legacy <c>DiseaseReviewsInfo.ProfileName</c>: the title and version, or nothing when the title is empty.</summary>
    private static string BuildProfileName(DiseaseReviewsReportRowDto row) =>
        row.Title.Length == 0 ? string.Empty : $"{row.Title} v{row.VersionMajor}.{row.VersionMinor}";

    private static DiseaseReviewsReportSectionView BuildSectionView(DiseaseReviewsReportRowDto row) => new(
        $"{row.SectionNumber}: {row.SectionName}",
        FormatDue(row.NextPolicyReview),
        FormatDue(row.NextAuthorReview),
        FormatDue(row.NextVaReview));

    private static string FormatDue(DateTime? date) => date is { } value ? Format(value, DueDateFormat) : string.Empty;

    private static string Format(DateTime date, string format) => date.ToString(format, CultureInfo.InvariantCulture);
}
