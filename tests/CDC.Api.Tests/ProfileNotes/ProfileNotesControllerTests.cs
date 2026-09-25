using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileNotes;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileNotes;

public class ProfileNotesControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileNotesController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetNoteTypes_ReturnsOk()
    {
        IReadOnlyList<ProfileNoteTypeDto> noteTypes = [new ProfileNoteTypeDto { Id = ProfileNoteTestData.NoteTypeId, Name = "Comment" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNoteTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(noteTypes));

        var response = await CreateController().GetNoteTypes(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(noteTypes);
    }

    [Fact]
    public async Task GetNotesBySection_ReturnsOk()
    {
        IReadOnlyList<ProfileNoteDto> notes = [ProfileNoteTestData.ProfileNoteDto()];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNotesBySectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(notes));

        var response = await CreateController().GetNotesBySection(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(notes);
    }

    [Fact]
    public async Task GetNotesBySection_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNotesBySectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<IReadOnlyList<ProfileNoteDto>>("not found"));

        var response = await CreateController().GetNotesBySection(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetNotesByVersion_ReturnsOk()
    {
        IReadOnlyList<ProfileNoteDto> notes = [ProfileNoteTestData.ProfileNoteDto()];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNotesByVersionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(notes));

        var response = await CreateController().GetNotesByVersion(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(notes);
    }

    [Fact]
    public async Task GetNotesByVersion_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetNotesByVersionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<IReadOnlyList<ProfileNoteDto>>("not found"));

        var response = await CreateController().GetNotesByVersion(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateNotes_ReturnsOk()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };
        var resultDto = new ProfileNoteChangesetResultDto { IdInsertList = [ProfileNoteTestData.NoteId] };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().UpdateNotes(command, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task UpdateNotes_ReturnsConflict()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Deletes = [new ProfileNoteDeleteDto { Id = ProfileNoteTestData.NoteId, LastUpdated = ProfileNoteTestData.RowVersion }]
        };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<ProfileNoteChangesetResultDto>("edited by another user"));

        var response = await CreateController().UpdateNotes(command, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status409Conflict);
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
