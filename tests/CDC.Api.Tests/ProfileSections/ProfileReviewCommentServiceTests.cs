using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileReviewCommentServiceTests
{
    private static readonly Guid AuditUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid VersionId = ProfileSectionTestData.ProfileVersionId;
    private static readonly Guid SectionId = ProfileSectionTestData.SectionId;
    private static readonly DateTime FixedNow = new(2026, 10, 13, 9, 30, 0);
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly Mock<IProfileSectionRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileVersionGuard> versions = new();
    private readonly Mock<IUserContext> userContext = new();

    public ProfileReviewCommentServiceTests()
    {
        userContext.SetupGet(context => context.UserId).Returns(AuditUserId);
        userContext.SetupGet(context => context.IsProfileEditor).Returns(true);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(false);
    }

    private ProfileReviewCommentService CreateService() =>
        new(
            repository.Object,
            versions.Object,
            userContext.Object,
            NullLogger<ProfileReviewCommentService>.Instance,
            new FixedTimeProvider(FixedNow));

    private void GivenVersion(bool isPublished = false, DateTime? effectiveDateTo = null) =>
        versions
            .Setup(guard => guard.FindAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionFacts(ProfileSectionTestData.ProfileId, isPublished, effectiveDateTo is null));

    private void GivenActingUser(string userName = "jdoe") =>
        repository
            .Setup(repo => repo.GetReviewCommentUserAsync(AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewCommentUser { UserName = userName, FullName = "Jane Doe" });

    private void GivenComments(params ProfileReviewComment[] comments) =>
        repository
            .Setup(repo => repo.GetProfileSectionReviewCommentsAsync(VersionId, SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);

    private static ProfileReviewComment Comment(string userName = "jdoe", int childCount = 0, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CommentSubject = "Subject",
        CommentReference = "Ref",
        CommentText = "Text",
        UserName = userName,
        UserFullName = "Some User",
        Level = 1,
        ChildCount = childCount,
        LastUpdated = RowVersion
    };

    [Fact]
    public async Task GetAsync_MapsComments_AndFlagsOnlyTheActingUsersOwnReplyFreeCommentsAsEditable()
    {
        GivenVersion();
        GivenActingUser();
        var own = Comment("JDOE");
        var withReplies = Comment("jdoe", childCount: 1);
        var someoneElses = Comment("asmith");
        GivenComments(own, withReplies, someoneElses);

        var result = await CreateService().GetAsync(VersionId, SectionId, CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Id.Should().Be(own.Id);
        result[0].CommentSubject.Should().Be("Subject");
        result[0].LastUpdated.Should().Equal(RowVersion);
        result[0].CanEdit.Should().BeTrue();
        result[0].CanDelete.Should().BeTrue();
        result[1].CanEdit.Should().BeFalse();
        result[2].CanEdit.Should().BeFalse();
        result[2].CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_FlagsNothingEditable_WhenTheVersionIsPublished()
    {
        GivenVersion(isPublished: true);
        GivenComments(Comment());

        var result = await CreateService().GetAsync(VersionId, SectionId, CancellationToken.None);

        result.Should().ContainSingle().Which.CanEdit.Should().BeFalse();
        repository.Verify(repo => repo.GetReviewCommentUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_DoesNotLookUpAnything_WhenThereAreNoComments()
    {
        GivenComments();

        var result = await CreateService().GetAsync(VersionId, SectionId, CancellationToken.None);

        result.Should().BeEmpty();
        versions.Verify(guard => guard.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_Throws_WhenTheUserMayNotViewTheNotesOfTheProfile()
    {
        versions
            .Setup(guard => guard.EnsureCanViewNotesAsync(VersionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var act = () => CreateService().GetAsync(VersionId, SectionId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_FlagsNothingEditable_WhenTheActingUserCannotBeFound()
    {
        GivenVersion();
        repository
            .Setup(repo => repo.GetReviewCommentUserAsync(AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewCommentUser?)null);
        GivenComments(Comment());

        var result = await CreateService().GetAsync(VersionId, SectionId, CancellationToken.None);

        result.Should().ContainSingle().Which.CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_InsertsATopLevelComment_AttributedToTheActingUser()
    {
        GivenVersion();
        GivenActingUser();
        NewProfileReviewComment? inserted = null;
        repository
            .Setup(repo => repo.InsertReviewCommentAsync(It.IsAny<NewProfileReviewComment>(), It.IsAny<CancellationToken>()))
            .Callback<NewProfileReviewComment, CancellationToken>((comment, _) => inserted = comment)
            .ReturnsAsync(RowVersion);

        var result = await CreateService().AddAsync(VersionId, SectionId, null, " Subject ", " Ref ", "<p>Text</p>", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LastUpdated.Should().Equal(RowVersion);
        inserted.Should().NotBeNull();
        result.Value.Id.Should().Be(inserted!.Id);
        inserted.ProfileVersionId.Should().Be(VersionId);
        inserted.ProfileSectionId.Should().Be(SectionId);
        inserted.ParentId.Should().BeNull();
        inserted.CommentDate.Should().Be(FixedNow);
        inserted.Subject.Should().Be("Subject");
        inserted.CommentReference.Should().Be("Ref");
        inserted.CommentText.Should().Be("<p>Text</p>");
        inserted.UserName.Should().Be("jdoe");
    }

    [Fact]
    public async Task AddAsync_InsertsAReply_WhenTheParentExists()
    {
        GivenVersion();
        GivenActingUser();
        var parent = Comment("asmith");
        GivenComments(parent);
        repository
            .Setup(repo => repo.InsertReviewCommentAsync(
                It.Is<NewProfileReviewComment>(comment => comment.ParentId == parent.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RowVersion);

        var result = await CreateService().AddAsync(VersionId, SectionId, parent.Id, "Re: Subject", "Ref", "Text", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_ReturnsNotFound_WhenTheParentDoesNotExist()
    {
        GivenVersion();
        GivenActingUser();
        GivenComments(Comment());

        var result = await CreateService().AddAsync(VersionId, SectionId, Guid.NewGuid(), "S", "R", "T", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task AddAsync_ReturnsForbidden_WhenTheVersionIsNotAnEditableDraft()
    {
        GivenVersion(isPublished: true);

        var result = await CreateService().AddAsync(VersionId, SectionId, null, "S", "R", "T", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(
            repo => repo.InsertReviewCommentAsync(It.IsAny<NewProfileReviewComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_ReturnsForbidden_WhenTheActingUserCannotBeFound()
    {
        GivenVersion();
        repository
            .Setup(repo => repo.GetReviewCommentUserAsync(AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewCommentUser?)null);

        var result = await CreateService().AddAsync(VersionId, SectionId, null, "S", "R", "T", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task AddAsync_ReturnsForbidden_WhenTheRequestHasNoKnownUser()
    {
        userContext.SetupGet(context => context.UserId).Returns((Guid?)null);
        GivenVersion();

        var result = await CreateService().AddAsync(VersionId, SectionId, null, "S", "R", "T", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.GetReviewCommentUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddAsync_ReturnsForbidden_ForAnExternalAuthorOrReviewer(bool isContributor)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);
        userContext
            .Setup(context => context.GetProfileRole(ProfileSectionTestData.ProfileId))
            .Returns(new ProfileRoleAssignment(ProfileSectionTestData.ProfileId, Guid.NewGuid(), "Policy author", isContributor));
        GivenVersion();

        var result = await CreateService().AddAsync(VersionId, SectionId, null, "S", "R", "T", CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(
            repo => repo.InsertReviewCommentAsync(It.IsAny<NewProfileReviewComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAnOwnReplyFreeComment()
    {
        GivenVersion();
        GivenActingUser();
        var comment = Comment();
        GivenComments(comment);
        byte[] newRowVersion = [8, 7, 6, 5, 4, 3, 2, 1];
        repository
            .Setup(repo => repo.UpdateReviewCommentAsync(comment.Id, "New", "NewRef", "NewText", RowVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newRowVersion);

        var result = await CreateService().UpdateAsync(VersionId, SectionId, comment.Id, new ReviewCommentContent(" New ", " NewRef ", "NewText"), RowVersion, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(comment.Id);
        result.Value.LastUpdated.Should().Equal(newRowVersion);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenTheCommentDoesNotExist()
    {
        GivenComments(Comment());

        var result = await CreateService().UpdateAsync(VersionId, SectionId, Guid.NewGuid(), new ReviewCommentContent("S", "R", "T"), RowVersion, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Theory]
    [InlineData("asmith", 0)]
    [InlineData("jdoe", 2)]
    public async Task UpdateAsync_ReturnsForbidden_WhenTheCommentIsNotOwnedOrHasReplies(string owner, int childCount)
    {
        GivenVersion();
        GivenActingUser();
        var comment = Comment(owner, childCount);
        GivenComments(comment);

        var result = await CreateService().UpdateAsync(VersionId, SectionId, comment.Id, new ReviewCommentContent("S", "R", "T"), RowVersion, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task UpdateAsync_PropagatesAConcurrencyException()
    {
        GivenVersion();
        GivenActingUser();
        var comment = Comment();
        GivenComments(comment);
        repository
            .Setup(repo => repo.UpdateReviewCommentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var act = () => CreateService().UpdateAsync(VersionId, SectionId, comment.Id, new ReviewCommentContent("S", "R", "T"), RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task DeleteAsync_DeletesAnOwnReplyFreeComment()
    {
        GivenVersion();
        GivenActingUser();
        var comment = Comment();
        GivenComments(comment);
        repository.Setup(repo => repo.DeleteReviewCommentAsync(comment.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAsync(VersionId, SectionId, comment.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.DeleteReviewCommentAsync(comment.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenTheCommentDoesNotExist()
    {
        GivenComments();

        var result = await CreateService().DeleteAsync(VersionId, SectionId, Guid.NewGuid(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenTheCommentHasReplies()
    {
        GivenVersion();
        GivenActingUser();
        var comment = Comment(childCount: 1);
        GivenComments(comment);

        var result = await CreateService().DeleteAsync(VersionId, SectionId, comment.Id, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.DeleteReviewCommentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenTheUserIsNotAProfileEditor()
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);
        GivenVersion();
        var comment = Comment();
        GivenComments(comment);

        var result = await CreateService().DeleteAsync(VersionId, SectionId, comment.Id, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task DeleteAllAsync_DeletesEveryCommentOfTheSection_WhenTheVersionIsAnEditableCurrentDraft()
    {
        GivenVersion();
        repository
            .Setup(repo => repo.DeleteAllReviewCommentsAsync(VersionId, SectionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAllAsync(VersionId, SectionId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.DeleteAllReviewCommentsAsync(VersionId, SectionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DeleteAllAsync_ReturnsForbidden_WhenTheVersionIsPublishedOrNoLongerCurrent(bool isPublished, bool isSuperseded)
    {
        GivenVersion(isPublished, isSuperseded ? FixedNow.AddDays(-1) : null);

        var result = await CreateService().DeleteAllAsync(VersionId, SectionId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.DeleteAllReviewCommentsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAllAsync_ReturnsForbidden_WhenTheUserIsNotAProfileEditor()
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);
        GivenVersion();

        var result = await CreateService().DeleteAllAsync(VersionId, SectionId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    private sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(localNow, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
