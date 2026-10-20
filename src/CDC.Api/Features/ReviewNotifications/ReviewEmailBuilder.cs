using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Renders the review reminder email. Wording and the review window follow the legacy D2R2 email;
/// the markup is semantic HTML (real table headers, status shown as text as well as colour, GOV.UK
/// colours with sufficient contrast) because email clients ignore external stylesheets.
/// </summary>
public static partial class ReviewEmailBuilder
{
    /// <summary>The subject line, unchanged from the legacy email.</summary>
    public const string Subject = "D2R2 - Profile review request";

    private const int DueWindowDays = 14;
    private const string Font = "font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.5;color:#0b0c0c;";
    private const string CellStyle = "border:1px solid #b1b4b6;padding:8px;text-align:left;vertical-align:top;";

    /// <summary>Builds the email for one recipient.</summary>
    /// <param name="recipient">The user and the reviews due.</param>
    /// <param name="options">Link and support mailbox settings.</param>
    /// <param name="today">Today's date, used to decide between overdue and due soon.</param>
    public static ReviewEmailMessage Build(ReviewNotificationRecipient recipient, ReviewNotificationsOptions options, DateTime today)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>")
            .Append(Subject)
            .Append("</title></head><body style=\"").Append(Font).Append("\">");

        html.Append("<p>Dear ").Append(Encode(recipient.FullName)).Append(",</p>");
        html.Append("<p>Below are your approaching, overdue and completed author, VA and policy reviews. ")
            .Append("After completing a review, update its status for the profile section in D2R2. ")
            .Append("To do this, find the profile on the search page, select the browse link, open the revision dates tab ")
            .Append("and set the status to complete.</p>");

        foreach (var version in recipient.ProfileVersions.Where(v => v.Sections.Count > 0))
        {
            AppendProfileVersion(html, version, today);
        }

        AppendFooter(html, options);
        html.Append("</body></html>");

        return new ReviewEmailMessage(recipient.EmailAddress, recipient.FullName, Subject, html.ToString());
    }

    /// <summary>Removes markup that is stored in some profile titles and decodes any entities.</summary>
    /// <param name="value">The raw title.</param>
    public static string StripMarkup(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : WebUtility.HtmlDecode(TagPattern().Replace(value, string.Empty)).Trim();

    private static void AppendProfileVersion(StringBuilder html, ReviewProfileVersion version, DateTime today)
    {
        var title = string.IsNullOrWhiteSpace(version.ScenarioTitle)
            ? version.ProfileTitle
            : $"{version.ProfileTitle} ({version.ScenarioTitle})";

        html.Append("<h2 style=\"font-size:19px;margin:24px 0 8px;\">")
            .Append(Encode($"{title} - version {version.FullVersion} (draft)"))
            .Append("</h2>");

        html.Append("<table style=\"border-collapse:collapse;width:100%;\"><thead><tr>");
        foreach (var heading in new[] { "Section", "VA review", "Policy review", "Author review" })
        {
            html.Append("<th scope=\"col\" style=\"").Append(CellStyle).Append("\">").Append(heading).Append("</th>");
        }

        html.Append("</tr></thead><tbody>");
        foreach (var section in version.Sections.OrderBy(s => s.SectionNumber))
        {
            html.Append("<tr><th scope=\"row\" style=\"").Append(CellStyle).Append("\">")
                .Append(section.SectionNumber).Append(". ").Append(Encode(section.SectionName)).Append("</th>");
            AppendReviewCell(html, section.Technical, today);
            AppendReviewCell(html, section.Policy, today);
            AppendReviewCell(html, section.Author, today);
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static void AppendReviewCell(StringBuilder html, ReviewSchedule schedule, DateTime today)
    {
        html.Append("<td style=\"").Append(CellStyle).Append("\">");

        if (schedule.NextReview is { } date && date.Date < today.Date.AddDays(DueWindowDays))
        {
            html.Append(date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
            var (label, colour) = Describe(schedule.Status, date.Date, today.Date);
            if (label is not null)
            {
                html.Append("<br><strong style=\"color:").Append(colour).Append(";\">").Append(label).Append("</strong>");
            }
        }
        else
        {
            html.Append("Not due");
        }

        html.Append("</td>");
    }

    private static (string? Label, string Colour) Describe(string? status, DateTime date, DateTime today) => status switch
    {
        "Complete" => ("Completed", "#005a30"),
        "Active" or "Proposed" when date < today => ("Overdue", "#d4351c"),
        "Active" or "Proposed" => ("Due soon", "#594d00"),
        _ => (null, "#0b0c0c")
    };

    private static void AppendFooter(StringBuilder html, ReviewNotificationsOptions options)
    {
        if (Uri.TryCreate(options.InternalHomePageUrl, UriKind.Absolute, out var homePage)
            && (homePage.Scheme == Uri.UriSchemeHttps || homePage.Scheme == Uri.UriSchemeHttp))
        {
            html.Append("<p>Internal users can access D2R2 at <a href=\"").Append(Encode(homePage.AbsoluteUri)).Append("\">")
                .Append(Encode(homePage.AbsoluteUri)).Append("</a>.</p>");
        }

        var support = string.IsNullOrWhiteSpace(options.SupportMailbox) ? "the D2R2 mailbox" : Encode(options.SupportMailbox.Trim());
        html.Append("<p>If you have a problem accessing D2R2, or are not sure why you received this email, contact ")
            .Append(support).Append(".</p>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    [GeneratedRegex("<[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagPattern();
}
