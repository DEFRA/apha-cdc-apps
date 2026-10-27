using System.Globalization;
using System.Text;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Builds the review reminder email as GOV.UK Notify markdown (Notify does not render HTML or tables),
/// following the wording of the legacy D2R2 <c>ReviewEmail</c>.
/// </summary>
public static class ReviewEmailComposer
{
    /// <summary>Subject line, unchanged from the legacy email.</summary>
    public const string Subject = "D2R2 - Profile review request";

    private const int ReminderWindowDays = 14;

    /// <summary>Composes the reminder for one user.</summary>
    /// <param name="user">The user and their due profile sections.</param>
    /// <param name="today">The current date, used to label reviews overdue or due soon.</param>
    /// <param name="options">Link and contact details.</param>
    public static EmailMessage Compose(ReviewDueUser user, DateTime today, ReviewNotificationsOptions options)
    {
        var body = new StringBuilder();
        body.Append("Please find the list of approaching, overdue and completed author, technical and policy reviews below. ");
        body.Append("After completing a review please update the relevant review status for the profile section in the system. ");
        body.Append("This can be achieved by locating the profile on the search page, clicking the browse link and then selecting the revision dates tab. ");
        body.Append("Set the status to complete by using the drop down list.\n\n");

        var hasHomePage = TryGetWebUri(options.InternalHomePageUrl, out var homePage);

        foreach (var profile in user.Profiles)
        {
            AppendProfile(body, profile, today, hasHomePage ? homePage : null);
        }

        if (TryGetWebUri(options.ExternalHomePageUrl, out var externalHomePage))
        {
            body.Append("The D2R2 system can be accessed via the internet using the following link: ").Append(externalHomePage.AbsoluteUri).Append("\n\n");
        }

        if (hasHomePage)
        {
            body.Append("Internal users can access the D2R2 system using the following link: ").Append(homePage!.AbsoluteUri).Append("\n\n");
        }

        body.Append("If you have a problem accessing the D2R2 application (or aren't sure why you've received this email) please contact us by emailing the D2R2 mailbox: ");
        body.Append(Clean(options.SupportMailbox));

        return new EmailMessage(user.EmailAddress, Clean(user.FullName), Subject, body.ToString());
    }

    private static void AppendProfile(StringBuilder body, ReviewDueProfile profile, DateTime today, Uri? baseUri)
    {
        var title = Clean(string.IsNullOrEmpty(profile.ScenarioTitle)
            ? $"{profile.Title} - v {profile.Version} (Draft)"
            : $"{profile.Title} ({profile.ScenarioTitle}) - v {profile.Version} (Draft)");

        body.Append("## ").Append(title).Append("\n\n");

        if (baseUri is not null && profile.ProfileId != Guid.Empty)
        {
            // The page binds profileId from the querystring and opens the all-revision-dates tab for that profile.
            var link = new Uri(baseUri, $"/SurveillanceProfiles/EditProfileAllRevisionDates?profileId={profile.ProfileId}");
            body.Append(link.AbsoluteUri).Append("\n\n");
        }

        body.Append("Reviews due for ").Append(title).Append(":\n\n");

        foreach (var section in profile.Sections)
        {
            body.Append("* ").Append(section.Number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Clean(section.Name));

            var due = new[]
            {
                ("Author review", DescribeReview(section.Author, today)),
                ("Technical review", DescribeReview(section.Technical, today)),
                ("Policy review", DescribeReview(section.Policy, today))
            }
            .Where(r => r.Item2.Length > 0)
            .Select(r => $"{r.Item1} {r.Item2}")
            .ToList();

            if (due.Count > 0)
            {
                body.Append(" - ").Append(string.Join(", ", due));
            }

            body.Append('\n');
        }

        body.Append('\n');
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
            "Complete" => $"{date} (Completed)",
            "Active" or "Proposed" => reviewDate.Date < today.Date
                ? $"{date} (Overdue)"
                : $"{date} (Due soon)",
            _ => date
        };
    }

    private static bool TryGetWebUri(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    // Strips characters that Notify would treat as markup, links or HTML.
    private static string Clean(string value) =>
        value.Replace("\r", " ").Replace("\n", " ")
            .Replace("<", string.Empty).Replace(">", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty)
            .Replace("*", string.Empty).Replace("`", string.Empty);
}
