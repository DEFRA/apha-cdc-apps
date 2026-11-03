using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Maps each review type to the reviewer group that is emailed about it.</summary>
public static class ReviewerGroups
{
    /// <summary>Group notified of Policy reviews.</summary>
    public const string Policy = "Policy";

    /// <summary>Group notified of Author reviews.</summary>
    public const string AuthorTechnical = "AuthorTechnical";

    /// <summary>Group notified of Technical (Veterinary Advisor) reviews.</summary>
    public const string VeterinaryAdvisor = "VeterinaryAdvisor";

    /// <summary>Every group, in the order they are checked.</summary>
    public static readonly IReadOnlyList<string> All = [Policy, AuthorTechnical, VeterinaryAdvisor];

    private const int ReminderWindowDays = 14;

    /// <summary>Returns the groups with no roles configured.</summary>
    /// <param name="options">The configured reviewer groups.</param>
    public static IReadOnlyList<string> Unconfigured(ReviewNotificationsOptions options) =>
        [.. All.Where(group => !options.ReviewerGroups.TryGetValue(group, out var roles) || !roles.Any(r => !string.IsNullOrWhiteSpace(r)))];

    /// <summary>
    /// Keeps only the reviews the user is a member of the reviewer group for, dropping sections and profiles
    /// left with nothing due. Returns null when nothing remains.
    /// </summary>
    /// <param name="user">The user and everything due to them.</param>
    /// <param name="today">The current date.</param>
    /// <param name="options">The configured reviewer groups.</param>
    public static ReviewDueUser? FilterToGroups(ReviewDueUser user, DateTime today, ReviewNotificationsOptions options)
    {
        var profiles = new List<ReviewDueProfile>();

        foreach (var profile in user.Profiles)
        {
            var roles = profile.UserRoles ?? [];
            var sections = profile.Sections
                .Select(section => new ReviewDueSection(
                    section.Number,
                    section.Name,
                    InGroup(AuthorTechnical, roles, options) ? section.Author : Blank,
                    InGroup(VeterinaryAdvisor, roles, options) ? section.Technical : Blank,
                    InGroup(Policy, roles, options) ? section.Policy : Blank))
                .Where(section => IsDue(section.Author, today) || IsDue(section.Technical, today) || IsDue(section.Policy, today))
                .ToList();

            if (sections.Count > 0)
            {
                profiles.Add(profile with { Sections = sections });
            }
        }

        return profiles.Count == 0 ? null : user with { Profiles = profiles };
    }

    private static readonly ReviewItem Blank = new(null, null);

    private static bool InGroup(string group, IReadOnlyList<string> roles, ReviewNotificationsOptions options) =>
        options.ReviewerGroups.TryGetValue(group, out var groupRoles)
        && groupRoles.Any(g => roles.Contains(g, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// True when the review is not Complete and its date is before now plus two weeks (overdue included),
    /// matching the <c>vwReviewEmail*</c> views. A null status or date never qualifies.
    /// </summary>
    /// <param name="item">The review.</param>
    /// <param name="now">The current date and time.</param>
    internal static bool IsDue(ReviewItem item, DateTime now) =>
        item.NextReview is { } date && item.Status is { } status && status != "Complete" && date < now.AddDays(ReminderWindowDays);
}
