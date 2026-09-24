using CDC.Api.Domain.Common;
using CDC.Api.Features.Species;
using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Queries;
using CDC.Api.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.Species;

public class SpeciesControllerTests
{
    private static readonly Guid AuditUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private SpeciesController CreateController() => new(mediator.Object, new SpeciesAuditOptions(AuditUserId))
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetAllSpecies_ReturnsOk()
    {
        IReadOnlyList<SpeciesDto> species = [new SpeciesDto { Id = SpeciesTestData.SpeciesId, Description = "Cattle" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetAllSpeciesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(species));

        var response = await CreateController().GetAllSpecies(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeSameAs(species);
    }

    [Fact]
    public async Task GetMetadata_ReturnsOk()
    {
        var metadata = new SpeciesMetadataDto
        {
            Sections = [new SpeciesSectionMetadataDto { Id = SpeciesTestData.SectionId, Name = "Epidemiology" }]
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSpeciesMetadataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(metadata));

        var response = await CreateController().GetSpeciesMetadata(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(metadata);
    }

    [Fact]
    public async Task GetSpeciesAnswerData_ReturnsOk()
    {
        var answerData = new SpeciesAnswerDataDto { SpeciesId = SpeciesTestData.SpeciesId, SpeciesName = "Cattle" };

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetSpeciesAnswerDataQuery>(query => query.SpeciesId == SpeciesTestData.SpeciesId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(answerData));

        var response = await CreateController().GetSpeciesAnswerData(SpeciesTestData.SpeciesId, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(answerData);
    }

    [Fact]
    public async Task GetSpeciesAnswerData_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSpeciesAnswerDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<SpeciesAnswerDataDto>("Species not found."));

        var response = await CreateController().GetSpeciesAnswerData(SpeciesTestData.SpeciesId, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status404NotFound);
        problem.Detail.Should().Be("Species not found.");
    }

    [Fact]
    public async Task GetAllSelectedSpecies_ReturnsOk()
    {
        IReadOnlyList<SelectedSpeciesDto> species = [new SelectedSpeciesDto { DiseaseName = "Bovine tuberculosis" }];

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetAllSelectedSpeciesQuery>(query => query.DiseaseName == "Bovine tuberculosis"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(species));

        var response = await CreateController().GetAllSelectedSpecies("Bovine tuberculosis", CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(species);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerData_ReturnsOk()
    {
        var command = SpeciesTestData.UpdateCommand();
        var updateResult = new UpdateSpeciesAnswerDataResultDto
        {
            SpeciesId = SpeciesTestData.SpeciesId,
            LastUpdated = SpeciesTestData.NewRowVersion
        };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(updateResult));

        var response = await CreateController().UpdateSpeciesAnswerData(command, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(updateResult);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerData_ReturnsConflict_WhenAnotherUserHasSaved()
    {
        var command = SpeciesTestData.UpdateCommand();

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<UpdateSpeciesAnswerDataResultDto>("Edited by another user."));

        var response = await CreateController().UpdateSpeciesAnswerData(command, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("Edited by another user.");
    }

    [Fact]
    public async Task UpdateSpeciesAnswerData_ReturnsBadRequest_WhenValidationFails()
    {
        // The validation pipeline behaviour throws before the handler runs; the middleware then
        // converts that into a 400. This asserts the exception reaches the caller unhandled by
        // the controller, which is what lets the middleware shape the response.
        var command = new UpdateSpeciesAnswerDataCommand();

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException("A species id is required."));

        var act = async () => await CreateController().UpdateSpeciesAnswerData(command, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task GetSpeciesDetail_ReturnsOk()
    {
        var detail = new SpeciesDetailDto { Id = SpeciesTestData.SpeciesId, Name = "Dairy cattle" };

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetSpeciesDetailQuery>(query => query.SpeciesId == SpeciesTestData.SpeciesId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(detail));

        var response = await CreateController().GetSpeciesDetail(SpeciesTestData.SpeciesId, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(detail);
    }

    [Fact]
    public async Task GetSpeciesDetail_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSpeciesDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<SpeciesDetailDto>("Species not found."));

        var response = await CreateController().GetSpeciesDetail(SpeciesTestData.SpeciesId, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status404NotFound);
        problem.Detail.Should().Be("Species not found.");
    }

    [Fact]
    public async Task GetSpeciesValidParents_ReturnsOk()
    {
        IReadOnlyList<SpeciesValidParentDto> validParents = [new SpeciesValidParentDto { Id = SpeciesTestData.SectionId, Name = "Cattle" }];

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetSpeciesValidParentsQuery>(query => query.SpeciesId == SpeciesTestData.SpeciesId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(validParents));

        var response = await CreateController().GetSpeciesValidParents(SpeciesTestData.SpeciesId, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(validParents);
    }

    [Fact]
    public async Task UpdateSpeciesNameParent_ReturnsOk()
    {
        var updateResult = new UpdateSpeciesNameParentResultDto
        {
            SpeciesId = SpeciesTestData.SpeciesId,
            LastUpdated = SpeciesTestData.NewRowVersion
        };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateSpeciesNameParentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(updateResult));

        var request = new UpdateSpeciesNameParentRequestDto
        {
            SpeciesId = SpeciesTestData.SpeciesId,
            Name = "Dairy",
            ParentId = SpeciesTestData.SectionId,
            Reason = "Simplifying the name",
            LastUpdated = SpeciesTestData.RowVersion
        };

        var response = await CreateController().UpdateSpeciesNameParent(request, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(updateResult);
    }

    [Fact]
    public async Task UpdateSpeciesNameParent_SetsUserIdFromConfiguredAuditOptions_NotFromTheRequestBody()
    {
        UpdateSpeciesNameParentCommand? capturedCommand = null;

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateSpeciesNameParentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => capturedCommand = (UpdateSpeciesNameParentCommand)request)
            .ReturnsAsync(Result.Success(new UpdateSpeciesNameParentResultDto { SpeciesId = SpeciesTestData.SpeciesId }));

        var request = new UpdateSpeciesNameParentRequestDto
        {
            SpeciesId = SpeciesTestData.SpeciesId,
            Name = "Dairy",
            Reason = "Simplifying the name",
            LastUpdated = SpeciesTestData.RowVersion
        };

        await CreateController().UpdateSpeciesNameParent(request, CancellationToken.None);

        capturedCommand.Should().NotBeNull();
        capturedCommand!.UserId.Should().Be(AuditUserId);
    }

    [Fact]
    public async Task UpdateSpeciesNameParent_ReturnsConflict_WhenAnotherUserHasSaved()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateSpeciesNameParentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<UpdateSpeciesNameParentResultDto>("Edited by another user."));

        var request = new UpdateSpeciesNameParentRequestDto
        {
            SpeciesId = SpeciesTestData.SpeciesId,
            Name = "Dairy",
            Reason = "Simplifying the name",
            LastUpdated = SpeciesTestData.RowVersion
        };

        var response = await CreateController().UpdateSpeciesNameParent(request, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("Edited by another user.");
    }

    [Fact]
    public async Task GetSpeciesAuditTrail_ReturnsOk()
    {
        IReadOnlyList<SpeciesAuditTrailEntryDto> auditTrail = [new SpeciesAuditTrailEntryDto { Id = SpeciesTestData.FieldId }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSpeciesAuditTrailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(auditTrail));

        var response = await CreateController().GetSpeciesAuditTrail(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(auditTrail);
    }

    private static ProblemDetails AssertProblem(ActionResult? result, int expectedStatusCode)
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
