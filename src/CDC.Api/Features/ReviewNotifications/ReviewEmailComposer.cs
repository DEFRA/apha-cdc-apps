using System.Globalization;
using System.Text;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Builds the <c>((body))</c> of the review reminder as GOV.UK Notify markdown, following the legacy D2R2
/// <c>ReviewEmail</c>. The Notify template holds the greeting, the instructions and the support mailbox. Notify cannot
/// show tables, so each legacy table row is a bullet. The comparison report is a link that CDC.Web generates, not an attachment.
/// </summary>
public static class ReviewEmailComposer
{
    /// <summary>Subject line, unchanged from the legacy email.</summary>
    public const string Subject = "D2R2 - Profile review request";

    /// <summary>CDC.Web page that lists every revision date of a profile.</summary>
    internal const string RevisionDatesPath = "/SurveillanceProfiles/EditProfileAllRevisionDates";

    /// <summary>CDC.Web handler that builds the draft-against-published comparison PDF when the link is opened.</summary>
    internal const string ComparisonPath = "/DiseaseProfiles/CompareVersions";

    private const int ReminderWindowDays = 14;

    private const string DateFormat = "d MMMM yyyy";

    /// <summary>Composes the reminder for one user.</summary>
    /// <param name="user">The user and their due profile sections.</param>
    /// <param name="now">The current date and time, used to decide which reviews are due or overdue.</param>
    /// <param name="options">Link details.</param>
    public static EmailMessage Compose(ReviewDueUser user, DateTime now, ReviewNotificationsOptions options)
    {
        var body = new StringBuilder();
        var hasHomePage = TryGetWebUri(options.InternalHomePageUrl, out var homePage);

        foreach (var profile in user.Profiles)
        {
            AppendProfile(body, profile, now, hasHomePage ? homePage : null);
        }

        if (TryGetWebUri(options.ExternalHomePageUrl, out var externalHomePage))
        {
            body.Append("External users can access [D2R2 on the internet](").Append(externalHomePage.AbsoluteUri).Append(").\n\n");
        }

        if (hasHomePage)
        {
            body.Append("Internal users can access [D2R2 on the Defra network](").Append(homePage!.AbsoluteUri).Append(").\n\n");
        }

        return new EmailMessage(user.EmailAddress, Clean(user.FullName), Subject, body.ToString().TrimEnd());
    }

    private static void AppendProfile(StringBuilder body, ReviewDueProfile profile, DateTime now, Uri? baseUri)
    {
        var title = Clean(string.IsNullOrEmpty(profile.ScenarioTitle)
            ? $"{profile.Title} - version {profile.Version} (draft)"
            : $"{profile.Title} ({profile.ScenarioTitle}) - version {profile.Version} (draft)");

        body.Append("## ").Append(title).Append("\n\n");

        foreach (var section in profile.Sections)
        {
            // Same column order as the legacy table.
            var reviews = new[]
                {
                    DescribeReview("VA", section.Technical, now),
                    DescribeReview("policy", section.Policy, now),
                    DescribeReview("author", section.Author, now)
                }
                .Where(text => text.Length > 0)
                .ToList();

            if (reviews.Count == 0)
            {
                continue;
            }

            body.Append("* ").Append(section.Number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Clean(section.Name))
                .Append(" - ").Append(string.Join(", ", reviews)).Append('\n');
        }

        body.Append('\n');

        if (baseUri is null || profile.ProfileId == Guid.Empty)
        {
            return;
        }

        var reviewDates = new Uri(baseUri, $"{RevisionDatesPath}?profileId={profile.ProfileId}");
        body.Append("[Open the revision dates for this profile](").Append(reviewDates.AbsoluteUri).Append(")\n\n");

        if (profile.HasPublishedVersion)
        {
            var comparison = new Uri(baseUri, $"{ComparisonPath}?handler=Review&profileId={profile.ProfileId}");
            body.Append("[Download the changes since the published version (PDF)](").Append(comparison.AbsoluteUri).Append(")\n\n");
        }
    }

    // As legacy, completed reviews in the window are listed too; the state is text, not colour.
    private static string DescribeReview(string type, ReviewItem item, DateTime now)
    {
        if (item.NextReview is not { } date || item.Status is null || date >= now.AddDays(ReminderWindowDays))
        {
            return string.Empty;
        }

        var state = item.Status == "Complete" ? "completed"
            : date.Date < now.Date ? "overdue"
            : "due soon";

        return $"{type} review {date.ToString(DateFormat, CultureInfo.InvariantCulture)} ({state})";
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
