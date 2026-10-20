using System.Data.Common;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IReviewNotificationRepository"/>. The views already apply the
/// rules (subscribed user, draft version, review not complete and due within two weeks), unchanged
/// from the legacy D2R2 review email.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReviewNotificationRepository(IDbConnectionFactory connectionFactory, ILogger<ReviewNotificationRepository> logger)
    : IReviewNotificationRepository
{
    internal const string UsersQuery =
        "SELECT Id, FullName, EmailAddress FROM dbo.vwReviewEmailUsers";

    internal const string ProfilesQuery =
        "SELECT UserId, ProfileVersionId, ProfileTitle, ScenarioTitle, VersionMajor, VersionMinor FROM dbo.vwReviewEmailProfiles";

    internal const string SectionsQuery =
        "SELECT UserId, ProfileVersionId, ShortName, SectionNumber, " +
        "AuthorReviewStatus, NextAuthorReview, TechnicalReviewStatus, NextTechnicalReview, " +
        "PolicyReviewStatus, NextPolicyReview FROM dbo.vwReviewEmailProfileSections";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReviewNotificationRecipient>> GetRecipientsDueReviewAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var users = (await connection.QueryAsync<UserRow>(new CommandDefinition(UsersQuery, cancellationToken: cancellationToken))).ToList();
            var profiles = (await connection.QueryAsync<ProfileRow>(new CommandDefinition(ProfilesQuery, cancellationToken: cancellationToken))).ToList();
            var sections = (await connection.QueryAsync<SectionRow>(new CommandDefinition(SectionsQuery, cancellationToken: cancellationToken))).ToList();

            return Assemble(users, profiles, sections);
        }
        catch (DbException exception)
        {
            logger.DatabaseCallFailed(exception, "GetRecipientsDueReview");
            throw;
        }
    }

    private static List<ReviewNotificationRecipient> Assemble(List<UserRow> users, List<ProfileRow> profiles, List<SectionRow> sections)
    {
        var profilesByUser = profiles.ToLookup(p => p.UserId);
        var sectionsByVersion = sections.ToLookup(s => (s.UserId, s.ProfileVersionId));

        return users
            .OrderBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new ReviewNotificationRecipient(
                user.Id,
                user.FullName ?? string.Empty,
                user.EmailAddress ?? string.Empty,
                profilesByUser[user.Id]
                    .Select(profile => new ReviewProfileVersion(
                        profile.ProfileVersionId,
                        ReviewEmailBuilder.StripMarkup(profile.ProfileTitle),
                        string.IsNullOrWhiteSpace(profile.ScenarioTitle) ? null : ReviewEmailBuilder.StripMarkup(profile.ScenarioTitle),
                        $"{profile.VersionMajor}.{profile.VersionMinor}",
                        sectionsByVersion[(user.Id, profile.ProfileVersionId)]
                            .Select(ToSection)
                            .OrderBy(s => s.SectionNumber)
                            .ToList()))
                    .OrderBy(v => v.ProfileTitle, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();
    }

    private static ReviewSection ToSection(SectionRow row) => new(
        row.SectionNumber,
        row.ShortName ?? string.Empty,
        new ReviewSchedule(row.NextAuthorReview, row.AuthorReviewStatus),
        new ReviewSchedule(row.NextTechnicalReview, row.TechnicalReviewStatus),
        new ReviewSchedule(row.NextPolicyReview, row.PolicyReviewStatus));

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ReviewNotificationRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.DatabaseCallFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }

    private sealed record UserRow
    {
        public Guid Id { get; init; }

        public string? FullName { get; init; }

        public string? EmailAddress { get; init; }
    }

    private sealed record ProfileRow
    {
        public Guid UserId { get; init; }

        public Guid ProfileVersionId { get; init; }

        public string? ProfileTitle { get; init; }

        public string? ScenarioTitle { get; init; }

        public int VersionMajor { get; init; }

        public int VersionMinor { get; init; }
    }

    private sealed record SectionRow
    {
        public Guid UserId { get; init; }

        public Guid ProfileVersionId { get; init; }

        public string? ShortName { get; init; }

        public int SectionNumber { get; init; }

        public string? AuthorReviewStatus { get; init; }

        public DateTime? NextAuthorReview { get; init; }

        public string? TechnicalReviewStatus { get; init; }

        public DateTime? NextTechnicalReview { get; init; }

        public string? PolicyReviewStatus { get; init; }

        public DateTime? NextPolicyReview { get; init; }
    }
}
