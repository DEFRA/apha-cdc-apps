using System.Text;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Infrastructure.Pdf;
using CDC.Common.Comparison;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Builds the PDF the legacy review email attached: for each draft profile version in the reminder, the
/// differences from the profile's current published version, as <c>ProfileVersionComparisonReportPdf</c> produced.
/// </summary>
public sealed class ComparisonAttachmentBuilder(
    IProfileReportRepository reports,
    IProfileManagementRepository profiles,
    IComparisonDataSource dataSource,
    IPdfRenderer pdfRenderer,
    IOptions<NotifyOptions> notifyOptions,
    ILogger<ComparisonAttachmentBuilder> logger) : IComparisonAttachmentBuilder
{
    /// <summary>Notify rejects files over 2 MB.</summary>
    internal const int MaxFileBytes = 2 * 1024 * 1024;

    /// <inheritdoc />
    public async Task<EmailAttachment?> BuildAsync(ReviewDueUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(notifyOptions.Value.AttachmentTemplateId))
        {
            logger.AttachmentsNotConfigured();

            return null;
        }

        var comparisons = new List<(string Published, string Draft, ProfileVersionComparisonReport Report)>();

        foreach (var profile in user.Profiles)
        {
            var comparison = await CompareAsync(profile.ProfileVersionId, cancellationToken);

            if (comparison is not null)
            {
                comparisons.Add(comparison.Value);
            }
        }

        if (comparisons.Count == 0)
        {
            return null;
        }

        var html = ProfileVersionComparisonHtml.Render([.. comparisons.Select(item => item.Report)]);
        var pdf = await pdfRenderer.RenderLandscapeAsync(html, cancellationToken);

        if (pdf.Length > MaxFileBytes)
        {
            logger.ComparisonReportTooLarge(user.Id, pdf.Length);

            return null;
        }

        var fileName = comparisons.Count == 1
            ? $"{comparisons[0].Published} - {comparisons[0].Draft} Comparison.pdf"
            : "Profile version comparisons.pdf";

        logger.ComparisonAttached(comparisons.Count, user.Id);

        return new EmailAttachment(SafeFileName(fileName), pdf);
    }

    private async Task<(string Published, string Draft, ProfileVersionComparisonReport Report)?> CompareAsync(
        Guid draftVersionId,
        CancellationToken cancellationToken)
    {
        var draft = await reports.GetProfileVersionHeaderAsync(draftVersionId, cancellationToken);

        if (draft is null)
        {
            logger.ComparisonSkipped(draftVersionId, "the draft version was not found");

            return null;
        }

        var profile = await profiles.GetProfileAttributesAsync(draft.ProfileId, cancellationToken);

        if (profile is null || profile.CurrentPublishedProfileVersionId == Guid.Empty)
        {
            logger.ComparisonSkipped(draftVersionId, "the profile has no current published version");

            return null;
        }

        var published = await reports.GetProfileVersionHeaderAsync(profile.CurrentPublishedProfileVersionId, cancellationToken);

        if (published is null)
        {
            logger.ComparisonSkipped(draftVersionId, "the published version was not found");

            return null;
        }

        var publishedLabel = $"{published.Title} {published.VersionMajor}.{published.VersionMinor}";
        var draftLabel = $"{draft.Title} {draft.VersionMajor}.{draft.VersionMinor}";
        var report = await ProfileVersionComparisonBuilder.BuildAsync(
            dataSource,
            published.Id,
            publishedLabel,
            draft.Id,
            draftLabel,
            cancellationToken);

        return (publishedLabel, draftLabel, report);
    }

    // Titles may contain HTML or characters a file name cannot hold.
    private static string SafeFileName(string name)
    {
        var plain = new StringBuilder(name.Length);
        var inTag = false;

        foreach (var c in name)
        {
            if (c == '<')
            {
                inTag = true;
            }
            else if (c == '>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                plain.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }
        }

        return plain.ToString().Trim();
    }
}
