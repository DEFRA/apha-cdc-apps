using CDC.Api.Domain.Common;
using CDC.Api.Features.Users;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Dtos;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.Users;

public class UsersControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private UsersController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static ResolveExternalUserCommand Command() => new()
    {
        SsoUserIdExt = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Email = "user@example.com",
        FirstName = "Jane",
        LastName = "External",
        Organisation = "ACME Ltd"
    };

    private static ResolveInternalUserCommand InternalCommand() => new()
    {
        SsoUserIdInt = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal"
    };

    [Fact]
    public async Task ResolveExternalUser_ReturnsOk_OnSuccess()
    {
        var dto = new ExternalUserDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Jane External",
            EmailAddress = "user@example.com",
            Organisation = "ACME Ltd"
        };

        mediator
            .Setup(sender => sender.Send(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().ResolveExternalUser(Command(), CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ResolveExternalUser_ReturnsForbidden_WhenNotPermitted()
    {
        mediator
            .Setup(sender => sender.Send(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<ExternalUserDto>("not permitted"));

        var response = await CreateController().ResolveExternalUser(Command(), CancellationToken.None);

        var objectResult = response.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ResolveInternalUser_ReturnsOk_OnSuccess()
    {
        var dto = new InternalUserDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Jane Internal",
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        mediator
            .Setup(sender => sender.Send(InternalCommand(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().ResolveInternalUser(InternalCommand(), CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ResolveInternalUser_ReturnsNotFound_WhenNoUserMatches()
    {
        mediator
            .Setup(sender => sender.Send(InternalCommand(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<InternalUserDto>("not found"));

        var response = await CreateController().ResolveInternalUser(InternalCommand(), CancellationToken.None);

        var objectResult = response.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

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
