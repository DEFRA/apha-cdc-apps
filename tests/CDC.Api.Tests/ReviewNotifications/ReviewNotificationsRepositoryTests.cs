using System.Data;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;

namespace CDC.Api.Tests.ReviewNotifications;

public sealed class ReviewNotificationsRepositoryTests : IDisposable
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid VersionId = Guid.NewGuid();

    private readonly FakeDbConnection connection = new();

    public void Dispose() => connection.Dispose();

    private ReviewNotificationsRepository CreateRepository() => new(new StubConnectionFactory(connection));

    private void ScriptUsers(params object?[][] rows) => connection.Script(
        ReviewNotificationsRepository.UsersQuery,
        new FakeCommandScript { ResultSets = [new FakeResultSet(["Id", "FullName", "EmailAddress"], rows)] });

    private void ScriptProfiles(params object?[][] rows) => connection.Script(
        ReviewNotificationsRepository.ProfilesQuery,
        new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(["UserId", "ProfileVersionId", "ProfileTitle", "ScenarioTitle", "VersionMajor", "VersionMinor", "HasPublishedVersion"], rows)
            ]
        });

    private void ScriptSections(params object?[][] rows) => connection.Script(
        ReviewNotificationsRepository.SectionsQuery,
        new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    [
                        "UserId", "ProfileVersionId", "ShortName", "SectionNumber", "TechnicalReviewStatus",
                        "PolicyReviewStatus", "AuthorReviewStatus", "NextTechnicalReview", "NextPolicyReview", "NextAuthorReview"
                    ],
                    rows)
            ]
        });

    private void ScriptRoles(params object?[][] rows) => connection.Script(
        ReviewNotificationsRepository.RolesQuery,
        new FakeCommandScript { ResultSets = [new FakeResultSet(["UserId", "ProfileVersionId", "RoleName"], rows)] });

    [Fact]
    public async Task GetUsersDueReviewAsync_ReturnsEmpty_WhenNoUsersDue()
    {
        ScriptRoles();
        ScriptUsers();
        ScriptProfiles();
        ScriptSections();

        var result = await CreateRepository().GetUsersDueReviewAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersDueReviewAsync_GroupsProfilesAndSectionsPerUser()
    {
        var due = new DateTime(2026, 10, 25);
        ScriptRoles([UserId, VersionId, "Policy reviewer"], [Guid.NewGuid(), VersionId, "Technical author"]);
        ScriptUsers([UserId, "Ann Reviewer", "ann@example.test"]);
        ScriptProfiles(
            [UserId, VersionId, "Anthrax", null, (byte)2, (byte)1, true],
            [Guid.NewGuid(), Guid.NewGuid(), "Other user's profile", null, (byte)1, (byte)0, false]);
        ScriptSections(
            [UserId, VersionId, "Second", 2, "Active", null, null, due, null, null],
            [UserId, VersionId, "First", 1, null, "Proposed", "Complete", null, due, due]);

        var result = await CreateRepository().GetUsersDueReviewAsync(CancellationToken.None);

        var user = result.Should().ContainSingle().Subject;
        user.FullName.Should().Be("Ann Reviewer");
        user.EmailAddress.Should().Be("ann@example.test");
        var profile = user.Profiles.Should().ContainSingle().Subject;
        profile.Title.Should().Be("Anthrax");
        profile.Version.Should().Be("2.1");
        profile.HasPublishedVersion.Should().BeTrue();
        profile.UserRoles.Should().Equal("Policy reviewer");
        profile.Sections.Select(s => s.Name).Should().Equal("First", "Second");
        profile.Sections[0].Policy.Should().Be(new Features.ReviewNotifications.Dtos.ReviewItem(due, "Proposed"));
        profile.Sections[0].Author.Status.Should().Be("Complete");
        profile.Sections[1].Technical.NextReview.Should().Be(due);
    }

    [Fact]
    public async Task GetUsersDueReviewAsync_OmitsUsersWithNoSections()
    {
        ScriptRoles();
        ScriptUsers([UserId, "Ann", "ann@example.test"]);
        ScriptProfiles([UserId, VersionId, "Anthrax", "Scenario", (byte)1, (byte)0, false]);
        ScriptSections();

        var result = await CreateRepository().GetUsersDueReviewAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
