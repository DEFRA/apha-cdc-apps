using System.Globalization;
using System.Net;
using System.Text;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Builds the review reminder email, following the wording and layout of the legacy D2R2 <c>ReviewEmail</c>.</summary>
public static class ReviewEmailComposer
{
    /// <summary>Subject line, unchanged from the legacy email.</summary>
    public const string Subject = "D2R2 - Profile review request";

    private const int ReminderWindowDays = 14;
    private const string TableStyle = "border-collapse:collapse;margin-bottom:20px;";
    private const string CellStyle = "border:1px solid #b1b4b6;padding:5px 10px;text-align:left;vertical-align:top;";

    /// <summary>Composes the reminder for one user.</summary>
    /// <param name="user">The user and their due profile sections.</param>
    /// <param name="today">The current date, used to label reviews overdue or due soon.</param>
    /// <param name="options">Link and contact details.</param>
    public static EmailMessage Compose(ReviewDueUser user, DateTime today, ReviewNotificationsOptions options)
    {
        var html = new StringBuilder();
        html.Append("<html lang=\"en\"><head><meta charset=\"utf-8\"><title>").Append(Encode(Subject)).Append("</title></head>");
        html.Append("<body style=\"font-family:Arial,sans-serif;font-size:16px;line-height:1.4;color:#0b0c0c;\">");
        html.Append("<p>Dear ").Append(Encode(user.FullName)).Append(",</p>");
        html.Append("<p>Please find the list of approaching, overdue and completed author, technical and policy reviews below. ");
        html.Append("After completing a review please update the relevant review status for the profile section in the system. ");
        html.Append("This can be achieved by locating the profile on the search page, clicking the browse link and then selecting the revision dates tab. ");
        html.Append("Set the status to complete by using the drop down list.</p>");

        var hasHomePage = Uri.TryCreate(options.InternalHomePageUrl, UriKind.Absolute, out var homePage)
            && (homePage.Scheme == Uri.UriSchemeHttps || homePage.Scheme == Uri.UriSchemeHttp);

        foreach (var profile in user.Profiles)
        {
            AppendProfile(html, profile, today, hasHomePage ? homePage : null);
        }

        if (hasHomePage)
        {
            var url = Encode(homePage!.AbsoluteUri);
            html.Append("<p>Internal users can access the D2R2 system using the following link: <a href=\"").Append(url).Append("\">").Append(url).Append("</a></p>");
        }

        html.Append("<p>If you have a problem accessing the D2R2 application (or aren't sure why you've received this email) please contact us by emailing the D2R2 mailbox: ");
        html.Append(Encode(options.SupportMailbox)).Append("</p></body></html>");

        return new EmailMessage(user.EmailAddress, user.FullName, Subject, html.ToString());
    }

    private static void AppendProfile(StringBuilder html, ReviewDueProfile profile, DateTime today, Uri? baseUri)
    {
        var title = string.IsNullOrEmpty(profile.ScenarioTitle)
            ? $"{profile.Title} - v {profile.Version} (Draft)"
            : $"{profile.Title} ({profile.ScenarioTitle}) - v {profile.Version} (Draft)";

        html.Append("<h2 style=\"font-size:19px;margin:20px 0 10px;\">");

        if (baseUri is null)
        {
            html.Append(Encode(title));
        }
        else
        {
            // The target page enforces sign-in and profile authorisation.
            var link = new Uri(baseUri, $"/SurveillanceProfiles/EditProfileAllRevisionDates/{profile.ProfileVersionId}");
            html.Append("<a href=\"").Append(Encode(link.AbsoluteUri)).Append("\">").Append(Encode(title)).Append("</a>");
        }

        html.Append("</h2>");
        html.Append("<table style=\"").Append(TableStyle).Append("\"><caption style=\"text-align:left;font-weight:bold;\">Reviews due for ")
            .Append(Encode(title)).Append("</caption><thead><tr>");

        foreach (var heading in new[] { "Section", "Author review", "Technical review", "Policy review" })
        {
            html.Append("<th scope=\"col\" style=\"").Append(CellStyle).Append("\">").Append(heading).Append("</th>");
        }

        html.Append("</tr></thead><tbody>");

        foreach (var section in profile.Sections)
        {
            html.Append("<tr><th scope=\"row\" style=\"").Append(CellStyle).Append("\">")
                .Append(section.Number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Encode(section.Name)).Append("</th>");
            AppendReviewCell(html, section.Author, today);
            AppendReviewCell(html, section.Technical, today);
            AppendReviewCell(html, section.Policy, today);
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static void AppendReviewCell(StringBuilder html, ReviewItem item, DateTime today)
    {
        html.Append("<td style=\"").Append(CellStyle).Append("\">").Append(DescribeReview(item, today)).Append("</td>");
    }

    // The status is always written as text so it does not rely on colour alone.
    private static string DescribeReview(ReviewItem item, DateTime today)
    {
        if (item.NextReview is not { } reviewDate || reviewDate.Date >= today.Date.AddDays(ReminderWindowDays))
        {
            return string.Empty;
        }

        var date = reviewDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        return item.Status switch
        {
            "Complete" => $"{date} <strong>(Completed)</strong>",
            "Active" or "Proposed" => reviewDate.Date < today.Date
                ? $"{date} <strong>(Overdue)</strong>"
                : $"{date} <strong>(Due soon)</strong>",
            _ => date
        };
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
