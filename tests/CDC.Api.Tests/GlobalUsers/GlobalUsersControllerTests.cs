using CDC.Api.Domain.Common;
using CDC.Api.Features.GlobalUsers;
using CDC.Api.Features.GlobalUsers.Commands;
using CDC.Api.Features.GlobalUsers.Dtos;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Api.Features.GlobalUsers.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.GlobalUsers;

public class GlobalUsersControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private GlobalUsersController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static SaveGlobalUserRequest Request() => new()
    {
        Id = Guid.NewGuid(),
        UserName = @"DEFRA\jsmith",
        FullName = "Jo Smith",
        Organisation = "APHA",
        Role = GlobalUserRole.ProfileEditor,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    [Fact]
    public async Task GetGlobalUsers_ReturnsOk()
    {
        var page = new PagedResult<GlobalUserDto> { Items = [], PageNumber = 1, PageSize = 10, TotalRecords = 0 };
        mediator.Setup(sender => sender.Send(It.IsAny<GetGlobalUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(page));

        var response = await CreateController().GetGlobalUsers(1, 10, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(page);
    }

    [Fact]
    public async Task Lookup_ReturnsOk()
    {
        var lookup = new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.NewUser };
        mediator.Setup(sender => sender.Send(It.Is<LookupGlobalUserQuery>(query => query.UserName == @"D\a"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(lookup));

        var response = await CreateController().Lookup(@"D\a", CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(lookup);
    }

    [Fact]
    public async Task AddGlobalUser_SendsNewUserCommand_AndReturnsNoContent()
    {
        var request = Request();
        mediator.Setup(sender => sender.Send(
                It.Is<SaveGlobalUserCommand>(command => command.IsNew && command.Id == request.Id && command.Role == request.Role),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().AddGlobalUser(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateGlobalUser_UsesRouteId_AndMapsValidationFailureToBadRequest()
    {
        var id = Guid.NewGuid();
        mediator.Setup(sender => sender.Send(
                It.Is<SaveGlobalUserCommand>(command => !command.IsNew && command.Id == id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailed<Unit>("bad"));

        var response = await CreateController().UpdateGlobalUser(id, Request(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeleteGlobalUser_ReturnsConflict_WhenRowVersionMoved()
    {
        mediator.Setup(sender => sender.Send(It.IsAny<DeleteGlobalUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<Unit>("moved"));

        var response = await CreateController().DeleteGlobalUser(Guid.NewGuid(), [1], CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handlers_DelegateToTheService()
    {
        var service = new Mock<IGlobalUsersService>(MockBehavior.Strict);
        var page = new PagedResult<GlobalUserDto> { Items = [], PageNumber = 1, PageSize = 10, TotalRecords = 0 };
        var lookup = new GlobalUserLookupResult();
        var id = Guid.NewGuid();
        service.Setup(s => s.GetGlobalUsersAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(page);
        service.Setup(s => s.LookupAsync("u", It.IsAny<CancellationToken>())).ReturnsAsync(lookup);
        service.Setup(s => s.SaveAsync(It.IsAny<SaveGlobalUserCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Unit.Value));
        service.Setup(s => s.DeleteAsync(It.IsAny<DeleteGlobalUserCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Unit.Value));

        (await new GetGlobalUsersQueryHandler(service.Object).Handle(new GetGlobalUsersQuery(1, 10), CancellationToken.None)).Value.Should().BeSameAs(page);
        (await new LookupGlobalUserQueryHandler(service.Object).Handle(new LookupGlobalUserQuery("u"), CancellationToken.None)).Value.Should().BeSameAs(lookup);
        (await new SaveGlobalUserCommandHandler(service.Object).Handle(new SaveGlobalUserCommand(id, true, "u", "f", "o", GlobalUserRole.ProfileEditor, false, []), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new DeleteGlobalUserCommandHandler(service.Object).Handle(new DeleteGlobalUserCommand(id, []), CancellationToken.None)).IsSuccess.Should().BeTrue();
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
