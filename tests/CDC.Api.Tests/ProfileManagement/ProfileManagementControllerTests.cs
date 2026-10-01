using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileManagementController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetProfileAttributes_ReturnsOk()
    {
        var dto = ProfileManagementTestData.ProfileAttributesDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileAttributesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileAttributes(ProfileManagementTestData.ProfileId, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileAttributes_ReturnsNotFound_WhenProfileMissing()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileAttributesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileAttributesDto>("not found"));

        var response = await CreateController().GetProfileAttributes(ProfileManagementTestData.ProfileId, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateProfile_ReturnsCreated()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            Title = "Anthrax",
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CloneProfileVersionId = Guid.Empty,
            ParentId = Guid.Empty,
            ProfileStatusId = ProfileManagementTestData.ProfileStatusId
        };
        var resultDto = new CreateProfileResultDto { NewProfileId = command.Id, NewLastUpdated = ProfileManagementTestData.RowVersion };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().CreateProfile(command, CancellationToken.None);

        var created = response.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task UpdateProfileAttributes_ReturnsNoContent()
    {
        var command = new UpdateProfileAttributesCommand { Id = Guid.Empty, LastUpdated = ProfileManagementTestData.RowVersion };
        var resultDto = new UpdateProfileAttributesResultDto { NewLastUpdated = ProfileManagementTestData.NewRowVersion };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateProfileAttributesCommand>(c => c.Id == ProfileManagementTestData.ProfileId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().UpdateProfileAttributes(ProfileManagementTestData.ProfileId, command, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateProfileAttributes_ReturnsConflict()
    {
        var command = new UpdateProfileAttributesCommand { Id = Guid.Empty, LastUpdated = ProfileManagementTestData.RowVersion };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateProfileAttributesCommand>(c => c.Id == ProfileManagementTestData.ProfileId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<UpdateProfileAttributesResultDto>("edited by another user"));

        var response = await CreateController().UpdateProfileAttributes(ProfileManagementTestData.ProfileId, command, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteProfileVersion_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteProfileVersionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<DeleteProfileVersionResultDto>("not found"));

        var response = await CreateController().DeleteProfileVersion(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SetProfileVersionPublicAccess_ReturnsNoContent()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<SetProfileVersionPublicAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().SetProfileVersionPublicAccess(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateProfileStatus_ReturnsNotFound_WhenStatusUnknown()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateProfileStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<Unit>("not found"));

        var response = await CreateController().UpdateProfileStatus(
            ProfileManagementTestData.ProfileId,
            new UpdateProfileStatusCommand(Guid.Empty, ProfileManagementTestData.ProfileStatusId),
            CancellationToken.None);

        AssertProblem(response, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateProfileStatus_ReturnsNoContent_WhenStatusKnown()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateProfileStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().UpdateProfileStatus(
            ProfileManagementTestData.ProfileId,
            new UpdateProfileStatusCommand(Guid.Empty, ProfileManagementTestData.ProfileStatusId),
            CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetNewProfileDefaults_ReturnsOk()
    {
        var dto = new NewProfileDefaultsDto { Title = "Anthrax" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNewProfileDefaultsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetNewProfileDefaults(ProfileManagementTestData.ProfileVersionId, false, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetAffectedSpecies_ReturnsOk()
    {
        var dto = new AffectedSpeciesDto { SpeciesId = ProfileManagementTestData.SpeciesId, Name = "Cattle" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetAffectedSpeciesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetAffectedSpecies(ProfileManagementTestData.SpeciesId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileStatusTypes_ReturnsOk()
    {
        IReadOnlyList<ProfileStatusTypeDto> statusTypes = [new ProfileStatusTypeDto { Id = ProfileManagementTestData.ProfileStatusId }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileStatusTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(statusTypes));

        var response = await CreateController().GetProfileStatusTypes(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(statusTypes);
    }

    [Fact]
    public async Task CreateNewProfileVersion_ReturnsCreated()
    {
        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);
        var resultDto = new NewProfileVersionResultDto { NewProfileVersionId = ProfileManagementTestData.NewProfileVersionId };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().CreateNewProfileVersion(command, CancellationToken.None);

        response.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task CreateNewProfileVersion_ReturnsConflict()
    {
        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<NewProfileVersionResultDto>("not the latest version"));

        var response = await CreateController().CreateNewProfileVersion(command, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteProfileVersion_ReturnsOk()
    {
        var resultDto = new DeleteProfileVersionResultDto { IsProfileDeleted = false };

        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteProfileVersionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().DeleteProfileVersion(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(resultDto);
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
