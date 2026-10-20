using System.Data;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewNotificationRepositoryTests : IDisposable
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly Guid VersionA1 = Guid.NewGuid();
    private static readonly Guid VersionA2 = Guid.NewGuid();
    private static readonly Guid VersionB1 = Guid.NewGuid();

    private readonly FakeDbConnection connection = new();

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ReviewNotificationRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<ReviewNotificationRepository>.Instance);

    private void ScriptViews(FakeResultSet users, FakeResultSet profiles, FakeResultSet sections)
    {
        connection.Script(ReviewNotificationRepository.UsersQuery, new FakeCommandScript { ResultSets = [users] });
        connection.Script(ReviewNotificationRepository.ProfilesQuery, new FakeCommandScript { ResultSets = [profiles] });
        connection.Script(ReviewNotificationRepository.SectionsQuery, new FakeCommandScript { ResultSets = [sections] });
    }

    private static FakeResultSet Users(params object?[][] rows) => new(["Id", "FullName", "EmailAddress"], rows);

    private static FakeResultSet Profiles(params object?[][] rows) =>
        new(["UserId", "ProfileVersionId", "ProfileTitle", "ScenarioTitle", "VersionMajor", "VersionMinor"], rows);

    private static FakeResultSet Sections(params object?[][] rows) => new(
        [
            "UserId", "ProfileVersionId", "ShortName", "SectionNumber",
            "AuthorReviewStatus", "NextAuthorReview", "TechnicalReviewStatus", "NextTechnicalReview",
            "PolicyReviewStatus", "NextPolicyReview"
        ],
        rows);

    [Fact]
    public async Task GetRecipientsDueReviewAsync_GroupsProfilesAndSectionsByUser_InDisplayOrder()
    {
        var due = new DateTime(2026, 10, 1);
        ScriptViews(
            Users([UserB, "Zed", "zed@example.com"], [UserA, "Ann", "ann@example.com"]),
            Profiles(
                [UserA, VersionA1, "<p>Zebra fever</p>", null, 2, 1],
                [UserA, VersionA2, "Anthrax", "Cattle", 3, 0],
                [UserB, VersionB1, "Rabies", " ", 1, 4]),
            Sections(
                [UserA, VersionA1, "Risk", 10, "Active", due, "Proposed", null, "Complete", null],
                [UserA, VersionA1, "Introduction", 1, null, null, null, null, null, null],
                [UserB, VersionB1, "Other", 5, "Active", due, null, null, null, null]));

        var result = await CreateRepository().GetRecipientsDueReviewAsync(CancellationToken.None);

        result.Select(r => r.FullName).Should().Equal("Ann", "Zed");
        var ann = result[0];
        ann.UserId.Should().Be(UserA);
        ann.EmailAddress.Should().Be("ann@example.com");
        ann.ProfileVersions.Select(v => v.ProfileTitle).Should().Equal("Anthrax", "Zebra fever");
        ann.ProfileVersions[0].ScenarioTitle.Should().Be("Cattle");
        ann.ProfileVersions[0].FullVersion.Should().Be("3.0");
        ann.ProfileVersions[0].Sections.Should().BeEmpty();

        var zebra = ann.ProfileVersions[1];
        zebra.ScenarioTitle.Should().BeNull();
        zebra.Sections.Select(s => s.SectionNumber).Should().Equal(1, 10);
        var risk = zebra.Sections[1];
        risk.SectionName.Should().Be("Risk");
        risk.Author.Should().Be(new CDC.Api.Features.ReviewNotifications.Dtos.ReviewSchedule(due, "Active"));
        risk.Technical.Status.Should().Be("Proposed");
        risk.Policy.Status.Should().Be("Complete");

        var zed = result[1];
        zed.ProfileVersions.Should().ContainSingle().Which.ScenarioTitle.Should().BeNull();
        zed.ProfileVersions[0].Sections.Should().ContainSingle().Which.SectionNumber.Should().Be(5);
    }

    [Fact]
    public async Task GetRecipientsDueReviewAsync_ReturnsEmpty_WhenNoUsersAreDue()
    {
        ScriptViews(Users(), Profiles(), Sections());

        var result = await CreateRepository().GetRecipientsDueReviewAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecipientsDueReviewAsync_ToleratesNullNames()
    {
        ScriptViews(Users([UserA, null, null]), Profiles(), Sections());

        var result = await CreateRepository().GetRecipientsDueReviewAsync(CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Match<CDC.Api.Features.ReviewNotifications.Dtos.ReviewNotificationRecipient>(
            r => r.FullName == string.Empty && r.EmailAddress == string.Empty);
    }

    [Fact]
    public async Task GetRecipientsDueReviewAsync_RethrowsDatabaseFailures()
    {
        connection.Script(ReviewNotificationRepository.UsersQuery, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetRecipientsDueReviewAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRecipientsDueReviewAsync_RethrowsConnectionFailures()
    {
        connection.ThrowOnOpen = new FakeDbException("cannot open");

        var act = () => CreateRepository().GetRecipientsDueReviewAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetRecipientsDueReviewAsync_Throws_WhenFactoryDoesNotReturnDbConnection()
    {
        var repository = new ReviewNotificationRepository(new NonDbConnectionFactory(), NullLogger<ReviewNotificationRepository>.Instance);

        var act = () => repository.GetRecipientsDueReviewAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class NonDbConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new Mock<IDbConnection>().Object;
    }
}
