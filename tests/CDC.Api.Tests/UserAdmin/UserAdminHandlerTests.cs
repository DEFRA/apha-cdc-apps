using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using CDC.Api.Features.UserAdmin.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.UserAdmin;

public class UserAdminHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] NewRowVersion = [1, 2, 3, 4, 5, 6, 7, 9];

    private readonly Mock<IUserAdminService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetGlobalUsersQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<MaintainedUserDto> users = [User(subscribed: true)];

        service.Setup(svc => svc.GetGlobalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var result = await new GetGlobalUsersQueryHandler(service.Object).Handle(new GetGlobalUsersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(users);
    }

    [Fact]
    public async Task GetExternalUsersQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<MaintainedUserDto> users = [User(subscribed: false)];

        service.Setup(svc => svc.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var result = await new GetExternalUsersQueryHandler(service.Object).Handle(new GetExternalUsersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(users);
    }

    [Fact]
    public async Task GetMaintainedUserQueryHandler_ReturnsNotFound_WhenNoSuchUserExists()
    {
        service.Setup(svc => svc.GetUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync((MaintainedUserDto?)null);

        var result = await new GetMaintainedUserQueryHandler(service.Object).Handle(new GetMaintainedUserQuery(UserId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateReviewEmailSubscriptionCommandHandler_ReturnsTheStoredSubscriptionState(bool subscribed)
    {
        var command = Command(subscribed);
        var resultDto = new UpdateReviewEmailSubscriptionResultDto
        {
            UserId = UserId,
            SubscribedToReviewEmails = subscribed,
            LastUpdated = NewRowVersion
        };

        service.Setup(svc => svc.UpdateReviewEmailSubscriptionAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(resultDto);

        var result = await Handler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubscribedToReviewEmails.Should().Be(subscribed);
        result.Value.LastUpdated.Should().BeEquivalentTo(NewRowVersion);
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionCommandHandler_ReturnsNotFound_WhenNoSuchUserExists()
    {
        var command = Command(subscribed: true);

        service
            .Setup(svc => svc.UpdateReviewEmailSubscriptionAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateReviewEmailSubscriptionResultDto?)null);

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionCommandHandler_ReturnsConflict_WhenTheUserWasEditedConcurrently()
    {
        var command = Command(subscribed: true);

        service
            .Setup(svc => svc.UpdateReviewEmailSubscriptionAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateReviewEmailSubscriptionCommandValidator_AcceptsEitherSubscriptionState(bool subscribed) =>
        new UpdateReviewEmailSubscriptionCommandValidator().Validate(Command(subscribed)).IsValid.Should().BeTrue();

    [Fact]
    public void UpdateReviewEmailSubscriptionCommandValidator_RejectsAnEmptyUserId() =>
        new UpdateReviewEmailSubscriptionCommandValidator()
            .Validate(new UpdateReviewEmailSubscriptionCommand { UserId = Guid.Empty, LastUpdated = RowVersion })
            .IsValid.Should().BeFalse();

    [Fact]
    public void UpdateReviewEmailSubscriptionCommandValidator_RejectsAMalformedRowVersion() =>
        new UpdateReviewEmailSubscriptionCommandValidator()
            .Validate(new UpdateReviewEmailSubscriptionCommand { UserId = UserId, LastUpdated = [1, 2] })
            .IsValid.Should().BeFalse();

    private UpdateReviewEmailSubscriptionCommandHandler Handler() =>
        new(service.Object, NullLogger<UpdateReviewEmailSubscriptionCommandHandler>.Instance);

    private static UpdateReviewEmailSubscriptionCommand Command(bool subscribed) => new()
    {
        UserId = UserId,
        SubscribedToReviewEmails = subscribed,
        LastUpdated = RowVersion
    };

    private static MaintainedUserDto User(bool subscribed) => new()
    {
        Id = UserId,
        UserName = "jbloggs",
        FullName = "Joe Bloggs",
        Organisation = "APHA",
        SubscribedToReviewEmails = subscribed,
        LastUpdated = RowVersion
    };
}
