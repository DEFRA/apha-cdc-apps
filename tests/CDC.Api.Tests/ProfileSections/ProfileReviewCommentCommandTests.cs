using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileReviewCommentCommandTests
{
    private static readonly Guid VersionId = ProfileSectionTestData.ProfileVersionId;
    private static readonly Guid SectionId = ProfileSectionTestData.SectionId;
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly Mock<IProfileReviewCommentService> service = new(MockBehavior.Strict);

    private static AddProfileReviewCommentCommand ValidAdd() => new()
    {
        ProfileVersionId = VersionId,
        ProfileSectionId = SectionId,
        Subject = "Subject",
        CommentReference = "Ref",
        CommentText = "Text"
    };

    private static UpdateProfileReviewCommentCommand ValidUpdate() => new()
    {
        Id = Guid.NewGuid(),
        ProfileVersionId = VersionId,
        ProfileSectionId = SectionId,
        Subject = "Subject",
        CommentReference = "Ref",
        CommentText = "Text",
        LastUpdated = RowVersion
    };

    [Fact]
    public void AddValidator_AcceptsAValidCommentAndAReply()
    {
        var validator = new AddProfileReviewCommentCommandValidator();

        validator.Validate(ValidAdd()).IsValid.Should().BeTrue();
        validator.Validate(ValidAdd() with { ParentId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddValidator_ReportsTheLegacyMessages_ForMissingFields()
    {
        var result = new AddProfileReviewCommentCommandValidator().Validate(
            ValidAdd() with { Subject = " ", CommentReference = string.Empty, CommentText = string.Empty });

        result.Errors.Select(error => error.ErrorMessage).Should().BeEquivalentTo(
            "You must enter a subject or cancel",
            "You must enter a reference or cancel",
            "You must enter a comment or cancel");
    }

    [Fact]
    public void AddValidator_ReportsTheLegacyMessages_ForOverlongSubjectAndReference()
    {
        var result = new AddProfileReviewCommentCommandValidator().Validate(
            ValidAdd() with { Subject = new string('s', 256), CommentReference = new string('r', 256) });

        result.Errors.Select(error => error.ErrorMessage).Should().BeEquivalentTo(
            "A subject must be no longer than 255 characters",
            "A reference must be no longer than 255 characters");
    }

    [Fact]
    public void AddValidator_AcceptsExactlyTwoHundredAndFiftyFiveCharacters() =>
        new AddProfileReviewCommentCommandValidator()
            .Validate(ValidAdd() with { Subject = new string('s', 255), CommentReference = new string('r', 255) })
            .IsValid.Should().BeTrue();

    [Fact]
    public void AddValidator_RejectsEmptyIdentifiers()
    {
        var result = new AddProfileReviewCommentCommandValidator().Validate(
            ValidAdd() with { ProfileVersionId = Guid.Empty, ProfileSectionId = Guid.Empty, ParentId = Guid.Empty });

        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void UpdateValidator_AcceptsAValidCommand() =>
        new UpdateProfileReviewCommentCommandValidator().Validate(ValidUpdate()).IsValid.Should().BeTrue();

    [Fact]
    public void UpdateValidator_RejectsEmptyIdentifiersAndABadRowVersion()
    {
        var result = new UpdateProfileReviewCommentCommandValidator().Validate(
            ValidUpdate() with { Id = Guid.Empty, ProfileVersionId = Guid.Empty, ProfileSectionId = Guid.Empty, LastUpdated = [1, 2] });

        result.Errors.Should().HaveCount(4);
    }

    [Fact]
    public void UpdateValidator_ReportsTheLegacyMessages()
    {
        var result = new UpdateProfileReviewCommentCommandValidator().Validate(
            ValidUpdate() with { Subject = string.Empty, CommentReference = string.Empty, CommentText = string.Empty });

        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void DeleteValidator_RejectsEmptyIdentifiers()
    {
        var validator = new DeleteProfileReviewCommentCommandValidator();

        validator.Validate(new DeleteProfileReviewCommentCommand(Guid.NewGuid(), VersionId, SectionId)).IsValid.Should().BeTrue();
        validator.Validate(new DeleteProfileReviewCommentCommand(Guid.Empty, Guid.Empty, Guid.Empty)).Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddHandler_ForwardsEveryValueToTheService()
    {
        var parentId = Guid.NewGuid();
        var dto = new SaveProfileReviewCommentResultDto { Id = Guid.NewGuid(), LastUpdated = RowVersion };
        service
            .Setup(svc => svc.AddAsync(VersionId, SectionId, parentId, "Subject", "Ref", "Text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var result = await new AddProfileReviewCommentCommandHandler(service.Object)
            .Handle(ValidAdd() with { ParentId = parentId }, CancellationToken.None);

        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateHandler_ForwardsEveryValueToTheService()
    {
        var command = ValidUpdate();
        var dto = new SaveProfileReviewCommentResultDto { Id = command.Id, LastUpdated = RowVersion };
        service
            .Setup(svc => svc.UpdateAsync(VersionId, SectionId, command.Id, new ReviewCommentContent("Subject", "Ref", "Text"), RowVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var result = await new UpdateProfileReviewCommentCommandHandler(service.Object, NullLogger<UpdateProfileReviewCommentCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateHandler_MapsAConcurrencyExceptionToAConflict()
    {
        service
            .Setup(svc => svc.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ReviewCommentContent>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await new UpdateProfileReviewCommentCommandHandler(service.Object, NullLogger<UpdateProfileReviewCommentCommandHandler>.Instance)
            .Handle(ValidUpdate(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task DeleteHandler_ForwardsTheIdentifiersToTheService()
    {
        var commentId = Guid.NewGuid();
        service
            .Setup(svc => svc.DeleteAsync(VersionId, SectionId, commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var result = await new DeleteProfileReviewCommentCommandHandler(service.Object)
            .Handle(new DeleteProfileReviewCommentCommand(commentId, VersionId, SectionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
    [Fact]
    public void DeleteAllValidator_RejectsEmptyIdentifiers()
    {
        var validator = new DeleteAllProfileReviewCommentsCommandValidator();

        validator.Validate(new DeleteAllProfileReviewCommentsCommand(VersionId, SectionId)).IsValid.Should().BeTrue();
        validator.Validate(new DeleteAllProfileReviewCommentsCommand(Guid.Empty, Guid.Empty)).Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAllHandler_ForwardsTheIdentifiersToTheService()
    {
        service
            .Setup(svc => svc.DeleteAllAsync(VersionId, SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var result = await new DeleteAllProfileReviewCommentsCommandHandler(service.Object)
            .Handle(new DeleteAllProfileReviewCommentsCommand(VersionId, SectionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAllHandler_ReturnsTheServicesRefusal()
    {
        service
            .Setup(svc => svc.DeleteAllAsync(VersionId, SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<Unit>("not a draft"));

        var result = await new DeleteAllProfileReviewCommentsCommandHandler(service.Object)
            .Handle(new DeleteAllProfileReviewCommentsCommand(VersionId, SectionId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }
}

public class ProfileReviewCommentControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileSectionsController CreateController() => new(mediator.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task AddProfileReviewComment_ReturnsOk_WithTheResult()
    {
        var command = new AddProfileReviewCommentCommand
        {
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            ProfileSectionId = ProfileSectionTestData.SectionId,
            Subject = "S",
            CommentReference = "R",
            CommentText = "T"
        };
        var dto = new SaveProfileReviewCommentResultDto { Id = Guid.NewGuid() };
        mediator.Setup(sender => sender.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(dto));

        var response = await CreateController().AddProfileReviewComment(command, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateProfileReviewComment_ReturnsConflict_WhenEditedByAnotherUser()
    {
        var command = new UpdateProfileReviewCommentCommand
        {
            Id = Guid.NewGuid(),
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            ProfileSectionId = ProfileSectionTestData.SectionId,
            Subject = "S",
            CommentReference = "R",
            CommentText = "T",
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<SaveProfileReviewCommentResultDto>("edited by another user"));

        var response = await CreateController().UpdateProfileReviewComment(command, CancellationToken.None);

        response.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeleteProfileReviewComment_ReturnsNoContent_OnSuccess()
    {
        var commentId = Guid.NewGuid();
        mediator
            .Setup(sender => sender.Send(
                It.Is<DeleteProfileReviewCommentCommand>(command =>
                    command.Id == commentId
                    && command.ProfileVersionId == ProfileSectionTestData.ProfileVersionId
                    && command.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().DeleteProfileReviewComment(
            commentId, ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteProfileReviewComment_ReturnsForbidden_WhenTheCommentCannotBeDeleted()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteProfileReviewCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<Unit>("has replies"));

        var response = await CreateController().DeleteProfileReviewComment(
            Guid.NewGuid(), ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task DeleteAllProfileReviewComments_ReturnsNoContent_OnSuccess()
    {
        mediator
            .Setup(sender => sender.Send(
                It.Is<DeleteAllProfileReviewCommentsCommand>(command =>
                    command.ProfileVersionId == ProfileSectionTestData.ProfileVersionId
                    && command.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().DeleteAllProfileReviewComments(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteAllProfileReviewComments_ReturnsForbidden_WhenTheVersionIsNotAnEditableDraft()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<DeleteAllProfileReviewCommentsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<Unit>("not a draft"));

        var response = await CreateController().DeleteAllProfileReviewComments(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
