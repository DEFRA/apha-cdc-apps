using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Features.UserAdmin;
using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.UserAdmin;

public class ReviewEmailSubscriptionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] NewRowVersion = [1, 2, 3, 4, 5, 6, 7, 9];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateReviewEmailSubscriptionAsync_StoresTheChosenState(bool subscribed)
    {
        var repository = new Mock<IUserAdminRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.UpdateReviewEmailSubscriptionAsync(UserId, subscribed, RowVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRowVersion);

        var result = await Service(repository).UpdateReviewEmailSubscriptionAsync(
            new UpdateReviewEmailSubscriptionCommand
            {
                UserId = UserId,
                SubscribedToReviewEmails = subscribed,
                LastUpdated = RowVersion
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.SubscribedToReviewEmails.Should().Be(subscribed);
        result.LastUpdated.Should().BeEquivalentTo(NewRowVersion);
        repository.VerifyAll();
    }

    [Fact]
    public async Task GetGlobalUsersAsync_ReportsTheStoredSubscriptionState()
    {
        var repository = new Mock<IUserAdminRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MaintainedUser { Id = UserId, FullName = "Joe Bloggs", SubscribedToReviewEmails = true, LastUpdated = RowVersion }
            ]);

        var users = await Service(repository).GetGlobalUsersAsync(CancellationToken.None);

        users.Should().ContainSingle().Which.SubscribedToReviewEmails.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewNotificationsJob_OnlyTargetsRecipientsReturnedByTheSubscriptionFilteredQuery()
    {
        var repository = new Mock<IReviewNotificationRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetRecipientsDueReviewEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReviewEmailRecipient { Id = UserId, EmailAddress = "joe.bloggs@example.gov.uk" }]);

        var succeeded = await new ReviewNotificationsJob(repository.Object, NullLogger<ReviewNotificationsJob>.Instance)
            .RunAsync(CancellationToken.None);

        succeeded.Should().BeTrue();
        repository.VerifyAll();
    }

    [Fact]
    public async Task ReviewNotificationsJob_NotifiesNobody_WhenEveryUserIsUnsubscribed()
    {
        var repository = new Mock<IReviewNotificationRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetRecipientsDueReviewEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var succeeded = await new ReviewNotificationsJob(repository.Object, NullLogger<ReviewNotificationsJob>.Instance)
            .RunAsync(CancellationToken.None);

        succeeded.Should().BeTrue();
    }

    private static UserAdminService Service(Mock<IUserAdminRepository> repository) =>
        new(repository.Object, NullLogger<UserAdminService>.Instance);
}
