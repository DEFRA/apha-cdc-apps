using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Dtos;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ExternalUsers.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ExternalUsers;

public class ExternalUsersControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ExternalUsersController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetExternalUsers_ReturnsOk()
    {
        var page = new PagedResult<ExternalUserDto> { Items = [], PageNumber = 2, PageSize = 10, TotalRecords = 0 };
        mediator.Setup(sender => sender.Send(
                It.Is<GetExternalUsersQuery>(query => query.PageNumber == 2 && query.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(page));

        var response = await CreateController().GetExternalUsers(2, 10, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(page);
    }

    [Fact]
    public async Task SetReviewEmailSubscription_SendsCommand_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        mediator.Setup(sender => sender.Send(
                It.Is<SetExternalUserSubscriptionCommand>(command => command.Id == id && command.SubscribedToReviewEmails),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().SetReviewEmailSubscription(
            id, new SetReviewEmailSubscriptionRequest { SubscribedToReviewEmails = true }, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SetReviewEmailSubscription_ReturnsNotFound_WhenUserDoesNotExist()
    {
        mediator.Setup(sender => sender.Send(It.IsAny<SetExternalUserSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<Unit>("missing"));

        var response = await CreateController().SetReviewEmailSubscription(
            Guid.NewGuid(), new SetReviewEmailSubscriptionRequest(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetProfileAccess_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var access = new ExternalUserProfileAccessContract();
        mediator.Setup(sender => sender.Send(
                It.Is<GetExternalUserProfileAccessQuery>(query => query.UserId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(access));

        var response = await CreateController().GetProfileAccess(id, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(access);
    }

    [Fact]
    public async Task SaveProfileAccess_SendsCommand_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        mediator.Setup(sender => sender.Send(
                It.Is<SaveExternalUserProfileAccessCommand>(command =>
                    command.UserId == id && command.ProfileId == profileId && command.RoleId == roleId
                    && command.SectionPermissionIds.Contains(sectionId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().SaveProfileAccess(
            id,
            profileId,
            new SaveExternalUserProfileAccessContract { RoleId = roleId, SectionPermissionIds = [sectionId] },
            CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SaveProfileAccess_ReturnsConflict_WhenAccessWasChanged()
    {
        mediator.Setup(sender => sender.Send(It.IsAny<SaveExternalUserProfileAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<Unit>("moved"));

        var response = await CreateController().SaveProfileAccess(
            Guid.NewGuid(), Guid.NewGuid(), new SaveExternalUserProfileAccessContract(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteProfileAccess_SendsCommand_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        mediator.Setup(sender => sender.Send(
                It.Is<DeleteExternalUserProfileAccessCommand>(command => command.UserId == id && command.ProfileId == profileId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().DeleteProfileAccess(id, profileId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Handlers_DelegateToTheService()
    {
        var service = new Mock<IExternalUsersService>(MockBehavior.Strict);
        var page = new PagedResult<ExternalUserDto> { Items = [], PageNumber = 1, PageSize = 10, TotalRecords = 0 };
        var access = new ExternalUserProfileAccessContract();
        var id = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        service.Setup(s => s.GetExternalUsersAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(page);
        service.Setup(s => s.GetProfileAccessAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(access));
        service.Setup(s => s.SetReviewEmailSubscriptionAsync(It.IsAny<SetExternalUserSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));
        service.Setup(s => s.SaveProfileAccessAsync(It.IsAny<SaveExternalUserProfileAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));
        service.Setup(s => s.DeleteProfileAccessAsync(It.IsAny<DeleteExternalUserProfileAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        (await new GetExternalUsersQueryHandler(service.Object).Handle(new GetExternalUsersQuery(1, 10), CancellationToken.None)).Value.Should().BeSameAs(page);
        (await new GetExternalUserProfileAccessQueryHandler(service.Object).Handle(new GetExternalUserProfileAccessQuery(id), CancellationToken.None)).Value.Should().BeSameAs(access);
        (await new SetExternalUserSubscriptionCommandHandler(service.Object).Handle(new SetExternalUserSubscriptionCommand(id, true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new SaveExternalUserProfileAccessCommandHandler(service.Object).Handle(new SaveExternalUserProfileAccessCommand(id, profileId, Guid.NewGuid(), []), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new DeleteExternalUserProfileAccessCommandHandler(service.Object).Handle(new DeleteExternalUserProfileAccessCommand(id, profileId), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    /// <summary>Minimal factory so <c>ControllerBase.Problem()</c> works without the MVC pipeline.</summary>
    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new()
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }
}
