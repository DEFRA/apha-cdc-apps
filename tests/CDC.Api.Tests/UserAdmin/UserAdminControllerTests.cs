using CDC.Api.Domain.Common;
using CDC.Api.Features.UserAdmin;
using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.UserAdmin;

public class UserAdminControllerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    [Fact]
    public async Task GetGlobalUsers_ReturnsOk()
    {
        IReadOnlyList<MaintainedUserDto> users = [new MaintainedUserDto { Id = UserId }];
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetGlobalUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(users));

        var result = await Controller().GetGlobalUsers(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExternalUsers_ReturnsOk()
    {
        IReadOnlyList<MaintainedUserDto> users = [new MaintainedUserDto { Id = UserId }];
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetExternalUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(users));

        var result = await Controller().GetExternalUsers(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsNotFound_WhenNoSuchUserExists()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetMaintainedUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<MaintainedUserDto>("missing"));

        var result = await Controller().GetUser(UserId, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateReviewEmailSubscription_ReturnsOk()
    {
        var command = Command(UserId);
        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new UpdateReviewEmailSubscriptionResultDto
            {
                UserId = UserId,
                SubscribedToReviewEmails = true,
                LastUpdated = RowVersion
            }));

        var result = await Controller().UpdateReviewEmailSubscription(UserId, command, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }


    [Fact]
    public async Task UpdateReviewEmailSubscription_UsesTheRouteUserId()
    {
        UpdateReviewEmailSubscriptionCommand? sent = null;
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateReviewEmailSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<UpdateReviewEmailSubscriptionResultDto>>, CancellationToken>(
                (request, _) => sent = (UpdateReviewEmailSubscriptionCommand)request)
            .ReturnsAsync(Result.Success(new UpdateReviewEmailSubscriptionResultDto { UserId = UserId }));

        var result = await Controller().UpdateReviewEmailSubscription(
            UserId, Command(Guid.NewGuid()), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be(UserId);
    }

    private UserAdminController Controller() => new(mediator.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static UpdateReviewEmailSubscriptionCommand Command(Guid userId) => new()
    {
        UserId = userId,
        SubscribedToReviewEmails = true,
        LastUpdated = RowVersion
    };
}
