using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IReviewNotificationsRepository"/>, reading the same three
/// views the legacy <c>ReviewEmailService</c> used. The views apply the two-week reminder window
/// and the subscription, Draft-state and not-Complete filters.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
public sealed class ReviewNotificationsRepository(IDbConnectionFactory connectionFactory) : IReviewNotificationsRepository
{
    internal const string UsersQuery = "SELECT Id, FullName, EmailAddress FROM dbo.vwReviewEmailUsers";

    internal const string ProfilesQuery =
        "SELECT UserId, ProfileVersionId, ProfileTitle, ScenarioTitle, VersionMajor, VersionMinor FROM dbo.vwReviewEmailProfiles";

    internal const string SectionsQuery =
        "SELECT UserId, ProfileVersionId, ShortName, SectionNumber, TechnicalReviewStatus, PolicyReviewStatus, " +
        "AuthorReviewStatus, NextTechnicalReview, NextPolicyReview, NextAuthorReview FROM dbo.vwReviewEmailProfileSections";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReviewDueUser>> GetUsersDueReviewAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var users = (await connection.QueryAsync<UserRow>(new CommandDefinition(UsersQuery, cancellationToken: cancellationToken))).ToList();
        var profiles = (await connection.QueryAsync<ProfileRow>(new CommandDefinition(ProfilesQuery, cancellationToken: cancellationToken))).ToList();
        var sections = (await connection.QueryAsync<SectionRow>(new CommandDefinition(SectionsQuery, cancellationToken: cancellationToken))).ToList();

        var result = new List<ReviewDueUser>();

        foreach (var user in users)
        {
            var userProfiles = profiles
                .Where(p => p.UserId == user.Id)
                .Select(p => new ReviewDueProfile(
                    p.ProfileVersionId,
                    p.ProfileTitle,
                    p.ScenarioTitle,
                    $"{p.VersionMajor}.{p.VersionMinor}",
                    sections
                        .Where(s => s.UserId == user.Id && s.ProfileVersionId == p.ProfileVersionId)
                        .OrderBy(s => s.SectionNumber)
                        .Select(ToSection)
                        .ToList()))
                .Where(p => p.Sections.Count > 0)
                .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (userProfiles.Count > 0)
            {
                result.Add(new ReviewDueUser(user.Id, user.FullName, user.EmailAddress, userProfiles));
            }
        }

        return result;
    }

    private static ReviewDueSection ToSection(SectionRow row) => new(
        row.SectionNumber,
        row.ShortName,
        new ReviewItem(row.NextAuthorReview, row.AuthorReviewStatus),
        new ReviewItem(row.NextTechnicalReview, row.TechnicalReviewStatus),
        new ReviewItem(row.NextPolicyReview, row.PolicyReviewStatus));

    private sealed class UserRow
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string EmailAddress { get; set; } = string.Empty;
    }

    private sealed class ProfileRow
    {
        public Guid UserId { get; set; }

        public Guid ProfileVersionId { get; set; }

        public string ProfileTitle { get; set; } = string.Empty;

        public string? ScenarioTitle { get; set; }

        public byte VersionMajor { get; set; }

        public byte VersionMinor { get; set; }
    }

    private sealed class SectionRow
    {
        public Guid UserId { get; set; }

        public Guid ProfileVersionId { get; set; }

        public string ShortName { get; set; } = string.Empty;

        public int SectionNumber { get; set; }

        public string? TechnicalReviewStatus { get; set; }

        public string? PolicyReviewStatus { get; set; }

        public string? AuthorReviewStatus { get; set; }

        public DateTime? NextTechnicalReview { get; set; }

        public DateTime? NextPolicyReview { get; set; }

        public DateTime? NextAuthorReview { get; set; }
    }
}
