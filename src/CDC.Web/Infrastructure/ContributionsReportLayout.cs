using System.Globalization;
using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Lays out the contributions report data for printing, matching the legacy
/// <c>ContributionReportPdf</c>: a heading per draft version, then a table per profile section.
/// </summary>
public static class ContributionsReportLayout
{
    /// <summary>The legacy <c>GlobalSettings.PDFQualityStatementText</c> default, shown in every page footer.</summary>
    public const string QualityStatementText =
        "The D2R2 Quality Statement can be found in the footer on every page when browsing in the application or in the Help Using D2R2 section";

    private const string DateFormat = "dd/MM/yyyy";

    /// <summary>Builds the report's view model from the API data.</summary>
    /// <param name="report">The report data.</param>
    /// <returns>The laid-out report.</returns>
    public static ContributionsReportViewModel Build(ContributionsReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ContributionsReportViewModel(
            ProfileTitleHtmlFormatter.ToPlainText(report.ProfileTitle),
            [.. report.Versions.Select(version => new ContributionsReportVersionView(
                BuildVersionHeading(version),
                [.. version.Sections.Select(section => new ContributionsReportSectionView(
                    $"{section.SectionNumber}. {section.SectionName}",
                    [.. section.Contributions.Select(contribution => new ContributionsReportRowView(
                        contribution.FullName,
                        contribution.OrganisationName,
                        contribution.LastContributionDate.ToString(DateFormat, CultureInfo.InvariantCulture)))]))]))]);
    }

    /// <summary>Builds the page header and footer shown on every page.</summary>
    /// <param name="profileTitle">The profile's plain-text title.</param>
    /// <returns>The render options.</returns>
    public static PdfRenderOptions BuildPdfOptions(string profileTitle) => new()
    {
        HeaderHtml =
            "<div style=\"width:100%;padding:0 12.7mm;font-family:Arial,Helvetica,sans-serif;font-size:10px;text-align:center;\">" +
            "<div style=\"border-bottom:1px solid #000;padding-bottom:4px;\">Contributions report for:<br/>" +
            WebUtility.HtmlEncode(profileTitle) + "</div></div>",
        FooterHtml =
            "<div style=\"width:100%;font-family:Arial,Helvetica,sans-serif;font-size:8px;text-align:center;\">" +
            "<div style=\"font-size:9px;margin-bottom:4px;\">Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>" +
            WebUtility.HtmlEncode(QualityStatementText) + "</div>",
        MarginTopMm = 30,
        MarginBottomMm = 25
    };

    /// <summary>Builds the file name offered for download, matching the legacy "{profile}_Contributions".</summary>
    /// <param name="profileTitle">The profile's plain-text title.</param>
    /// <returns>A file name with characters that are invalid in file names replaced.</returns>
    public static string BuildFileName(string profileTitle)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeTitle = new string([.. profileTitle.Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character)]).Trim();

        return $"{(safeTitle.Length == 0 ? "Profile" : safeTitle)}_Contributions.pdf";
    }

    /// <summary>Legacy <c>ProfileVersionInfo.ToString()</c> for a draft version.</summary>
    private static string BuildVersionHeading(ContributionsReportVersionDto version)
    {
        var validity = version.EffectiveDateTo is { } to
            ? $"Valid from {version.EffectiveDateFrom.ToString(DateFormat, CultureInfo.InvariantCulture)} to {to.ToString(DateFormat, CultureInfo.InvariantCulture)}"
            : $"Valid from {version.EffectiveDateFrom.ToString(DateFormat, CultureInfo.InvariantCulture)}";

        var heading = $"{ProfileTitleHtmlFormatter.ToPlainText(version.FullTitle)} - v{version.VersionMajor}.{version.VersionMinor} (Draft) {validity}";

        return version.IsCurrent && version.ProfileStatusName.Length > 0
            ? $"{heading} ({version.ProfileStatusName})"
            : heading;
    }
}
