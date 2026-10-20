namespace CDC.Api.Features.ReviewNotifications.Dtos;

/// <summary>A user due a review reminder, with the draft profile versions and sections they are due to review.</summary>
/// <param name="UserId">The user's identifier (safe to log; the name and address are not).</param>
/// <param name="FullName">The user's full name.</param>
/// <param name="EmailAddress">The address the reminder is sent to.</param>
/// <param name="ProfileVersions">The draft profile versions with at least one review due.</param>
public sealed record ReviewNotificationRecipient(
    Guid UserId,
    string FullName,
    string EmailAddress,
    IReadOnlyList<ReviewProfileVersion> ProfileVersions);

/// <summary>A draft profile version containing sections due for review.</summary>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="ProfileTitle">The profile title, with any markup removed.</param>
/// <param name="ScenarioTitle">The scenario title, or null when the profile has none.</param>
/// <param name="FullVersion">The version in major.minor form.</param>
/// <param name="Sections">The sections due for review.</param>
public sealed record ReviewProfileVersion(
    Guid ProfileVersionId,
    string ProfileTitle,
    string? ScenarioTitle,
    string FullVersion,
    IReadOnlyList<ReviewSection> Sections);

/// <summary>A profile section with its three review dates and statuses.</summary>
/// <param name="SectionNumber">The section number.</param>
/// <param name="SectionName">The section short name.</param>
/// <param name="Author">The author review.</param>
/// <param name="Technical">The technical (VA) review.</param>
/// <param name="Policy">The policy review.</param>
public sealed record ReviewSection(
    int SectionNumber,
    string SectionName,
    ReviewSchedule Author,
    ReviewSchedule Technical,
    ReviewSchedule Policy);

/// <summary>The next date and status of one review type.</summary>
/// <param name="NextReview">The next review date, or null when none is scheduled.</param>
/// <param name="Status">The review status: Active, Proposed or Complete.</param>
public sealed record ReviewSchedule(DateTime? NextReview, string? Status);

/// <summary>A rendered email ready to send.</summary>
/// <param name="ToAddress">The recipient address.</param>
/// <param name="ToName">The recipient display name.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="HtmlBody">The HTML body.</param>
public sealed record ReviewEmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);
