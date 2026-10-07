using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileContributors;

public class ProfileContributorsControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileContributorsController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetProfileContributors_ReturnsOk()
    {
        var profileId = Guid.NewGuid();
        var page = new PagedResult<ContributorDto> { Items = [], PageNumber = 1, PageSize = 10, TotalRecords = 0 };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileContributorsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(page));

        var response = await CreateController().GetProfileContributors(profileId, 1, 10, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(page);
    }

    [Fact]
    public async Task GetProfileUserRoles_ReturnsOk()
    {
        IReadOnlyList<ProfileUserRoleDto> roles = [new ProfileUserRoleDto { Id = Guid.NewGuid(), Name = "Technical author", IsContributor = true }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileUserRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(roles));

        var response = await CreateController().GetProfileUserRoles(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(roles);
    }

    [Fact]
    public async Task GetContributorForEdit_ReturnsOk()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var dto = new ContributorEditDto { Id = contributorId, UserName = "carrie.batten" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetContributorForEditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetContributorForEdit(profileId, contributorId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetContributorForEdit_ReturnsNotFound()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetContributorForEditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ContributorEditDto>("Not found."));

        var response = await CreateController().GetContributorForEdit(profileId, contributorId, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateContributor_ReturnsNoContent()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var request = new UpdateContributorRequest
        {
            RoleId = Guid.NewGuid(),
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            SectionPermissionIds = [],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().UpdateContributor(profileId, contributorId, request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateContributor_ReturnsConflict()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var request = new UpdateContributorRequest
        {
            RoleId = Guid.NewGuid(),
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            SectionPermissionIds = [],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<Unit>("Stale."));

        var response = await CreateController().UpdateContributor(profileId, contributorId, request, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UpdateContributor_ReturnsBadRequest_WhenValidationFails()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var request = new UpdateContributorRequest
        {
            RoleId = Guid.NewGuid(),
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            SectionPermissionIds = [],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailed<Unit>("Please select a valid role."));

        var response = await CreateController().UpdateContributor(profileId, contributorId, request, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task VerifyContributorUsername_ReturnsOk()
    {
        var dto = new UserVerificationResultDto { Outcome = UserVerificationOutcome.ExistingUser, UserId = Guid.NewGuid() };

        mediator
            .Setup(sender => sender.Send(It.IsAny<VerifyContributorUsernameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().VerifyContributorUsername("internal\\carrie.batten", CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task AddContributor_ReturnsNoContent()
    {
        var profileId = Guid.NewGuid();
        var request = new AddContributorRequest
        {
            ContributorId = Guid.NewGuid(),
            UserName = "internal\\new.user",
            IsSsoUser = false,
            RoleId = Guid.NewGuid(),
            FullName = "New User",
            Organisation = "Pirbright Institute",
            SectionPermissionIds = []
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().AddContributor(profileId, request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AddContributor_ReturnsBadRequest_WhenValidationFails()
    {
        var profileId = Guid.NewGuid();
        var request = new AddContributorRequest
        {
            ContributorId = Guid.NewGuid(),
            UserName = "internal\\new.user",
            IsSsoUser = false,
            RoleId = Guid.NewGuid(),
            FullName = "New User",
            Organisation = "Pirbright Institute",
            SectionPermissionIds = []
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailed<Unit>("Please select a valid role."));

        var response = await CreateController().AddContributor(profileId, request, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeleteContributor_ReturnsNoContent()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();

        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().DeleteContributor(
            profileId, contributorId, [1, 2, 3, 4, 5, 6, 7, 8], CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteContributor_ReturnsConflict_WhenTheRowVersionHasMovedOn()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();

        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<Unit>("Edited by another user."));

        var response = await CreateController().DeleteContributor(
            profileId, contributorId, [1, 2, 3, 4, 5, 6, 7, 8], CancellationToken.None);

        AssertProblem(response, StatusCodes.Status409Conflict);
    }

    private static ProblemDetails AssertProblem(IActionResult result, int expectedStatusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);

        return objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
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
