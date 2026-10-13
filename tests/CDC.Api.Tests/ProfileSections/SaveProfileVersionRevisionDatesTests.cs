using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class SaveProfileVersionRevisionDatesTests
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly SaveProfileVersionRevisionDatesCommandValidator validator = new();

    private static ProfileSectionRevisionDatesInputDto Section(
        Guid? sectionId = null,
        DateTime? nextAuthorReview = null,
        byte[]? lastUpdated = null,
        Guid? authorFrequency = null) =>
        new()
        {
            ProfileSectionId = sectionId ?? ProfileSectionTestData.SectionId,
            NextAuthorReview = nextAuthorReview,
            AuthorReviewFrequencyId = authorFrequency,
            LastUpdated = lastUpdated ?? RowVersion
        };

    private static SaveProfileVersionRevisionDatesCommand Command(params ProfileSectionRevisionDatesInputDto[] sections) =>
        new() { ProfileVersionId = ProfileSectionTestData.ProfileVersionId, Sections = sections };

    [Fact]
    public void Validator_ValidRequest_ShouldPass()
    {
        var result = validator.Validate(Command(Section(nextAuthorReview: new DateTime(2027, 1, 1))));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyProfileVersionId_ShouldFail()
    {
        var result = validator.Validate(new SaveProfileVersionRevisionDatesCommand { ProfileVersionId = Guid.Empty, Sections = [Section()] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_NoSections_ShouldFail()
    {
        var result = validator.Validate(Command());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_DuplicateSection_ShouldFail()
    {
        var result = validator.Validate(Command(Section(), Section()));

        result.Errors.Should().Contain(error => error.ErrorMessage == "Each section may be submitted only once.");
    }

    [Fact]
    public void Validator_EmptySectionId_ShouldFail()
    {
        var result = validator.Validate(Command(Section(sectionId: Guid.Empty)));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void Validator_MissingOrWrongLengthRowVersion_ShouldFail(byte[]? rowVersion)
    {
        var command = new SaveProfileVersionRevisionDatesCommand
        {
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            Sections = [new ProfileSectionRevisionDatesInputDto { ProfileSectionId = ProfileSectionTestData.SectionId, LastUpdated = rowVersion! }]
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    [InlineData(2079, 6, 7)]
    public void Validator_DateOutsideSupportedRange_ShouldFail(int year, int month, int day)
    {
        var result = validator.Validate(Command(Section(nextAuthorReview: new DateTime(year, month, day))));

        result.Errors.Should().Contain(error => error.ErrorMessage.StartsWith("Next author review date must be between", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1900, 1, 1)]
    [InlineData(2079, 6, 6)]
    public void Validator_DateAtRangeBoundary_ShouldPass(int year, int month, int day)
    {
        var result = validator.Validate(Command(Section(nextAuthorReview: new DateTime(year, month, day))));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyGuidFrequency_ShouldFail()
    {
        var result = validator.Validate(Command(Section(authorFrequency: Guid.Empty)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_ReturnsSuccess_AndForwardsTheSections()
    {
        var service = new Mock<IProfileSectionService>(MockBehavior.Strict);
        var command = Command(Section());
        service
            .Setup(svc => svc.SaveProfileVersionRevisionDatesAsync(
                ProfileSectionTestData.ProfileVersionId, command.Sections, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveProfileRevisionDatesResultDto { SectionsSavedCount = 1 });

        var result = await new SaveProfileVersionRevisionDatesCommandHandler(service.Object, NullLogger<SaveProfileVersionRevisionDatesCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsSavedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handler_ReturnsConflict_WhenTheServiceReportsAConcurrentEdit()
    {
        var service = new Mock<IProfileSectionService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.SaveProfileVersionRevisionDatesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ProfileSectionRevisionDatesInputDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await new SaveProfileVersionRevisionDatesCommandHandler(service.Object, NullLogger<SaveProfileVersionRevisionDatesCommandHandler>.Instance)
            .Handle(Command(Section()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Handler_LetsValidationExceptionsPropagate_ForTheGlobalMiddleware()
    {
        var service = new Mock<IProfileSectionService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.SaveProfileVersionRevisionDatesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ProfileSectionRevisionDatesInputDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("invalid"));

        var act = () => new SaveProfileVersionRevisionDatesCommandHandler(service.Object, NullLogger<SaveProfileVersionRevisionDatesCommandHandler>.Instance)
            .Handle(Command(Section()), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Controller_ReturnsOk_WhenTheCommandSucceeds()
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        var dto = new SaveProfileRevisionDatesResultDto { SectionsSavedCount = 2 };
        mediator
            .Setup(sender => sender.Send(It.IsAny<SaveProfileVersionRevisionDatesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));
        var controller = new ProfileSectionsController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.SaveProfileVersionRevisionDates(Command(Section()), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task Controller_Returns409_WhenTheCommandConflicts()
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        mediator
            .Setup(sender => sender.Send(It.IsAny<SaveProfileVersionRevisionDatesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<SaveProfileRevisionDatesResultDto>("edited by another user"));
        var controller = new ProfileSectionsController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.SaveProfileVersionRevisionDates(Command(Section()), CancellationToken.None);

        response.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}
