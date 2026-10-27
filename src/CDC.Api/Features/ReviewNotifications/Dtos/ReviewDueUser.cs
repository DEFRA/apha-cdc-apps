namespace CDC.Api.Features.ReviewNotifications.Dtos;

/// <summary>A subscribed user with at least one profile section whose review is due, plus the detail the reminder lists.</summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="FullName">Display name used in the salutation.</param>
/// <param name="EmailAddress">Address the reminder is sent to.</param>
/// <param name="Profiles">Draft profile versions with sections due for review.</param>
public sealed record ReviewDueUser(
    Guid Id,
    string FullName,
    string EmailAddress,
    IReadOnlyList<ReviewDueProfile> Profiles);

/// <summary>A draft profile version containing sections due for review.</summary>
/// <param name="ProfileId">The profile identifier used by the profile page route.</param>
/// <param name="ProfileVersionId">The profile version identifier.</param>
/// <param name="Title">Profile title.</param>
/// <param name="ScenarioTitle">Optional scenario title.</param>
/// <param name="Version">Version formatted as major.minor.</param>
/// <param name="Sections">Sections assigned to the user for this version, in section order.</param>
/// <param name="UserRoles">Names of the roles the user holds on the profile; they decide which reviewer groups the user belongs to.</param>
public sealed record ReviewDueProfile(
    Guid ProfileId,
    Guid ProfileVersionId,
    string Title,
    string? ScenarioTitle,
    string Version,
    IReadOnlyList<ReviewDueSection> Sections,
    IReadOnlyList<string>? UserRoles = null);

/// <summary>A profile section and the state of its three review types.</summary>
/// <param name="Number">Section number.</param>
/// <param name="Name">Short section name.</param>
/// <param name="Author">Author review.</param>
/// <param name="Technical">Technical review.</param>
/// <param name="Policy">Policy review.</param>
public sealed record ReviewDueSection(
    int Number,
    string Name,
    ReviewItem Author,
    ReviewItem Technical,
    ReviewItem Policy);

/// <summary>The next date and status of one review type.</summary>
/// <param name="NextReview">Next review date, if scheduled.</param>
/// <param name="Status">Review status name: Active, Proposed or Complete; null when unspecified.</param>
public sealed record ReviewItem(DateTime? NextReview, string? Status);
