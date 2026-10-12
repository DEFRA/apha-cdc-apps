using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileSectionsControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileSectionsController CreateController() => new(mediator.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetProfileQuestionnaireMetadata_ReturnsOk()
    {
        var dto = new ProfileQuestionnaireMetadataDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileQuestionnaireMetadataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileQuestionnaireMetadata(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileSectionAnswers_ReturnsOk_AndForwardsBothIdentifiers()
    {
        var dto = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            ProfileSectionId = ProfileSectionTestData.SectionId
        };

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileSectionAnswersQuery>(query =>
                    query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId &&
                    query.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileSectionAnswers(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatus_ReturnsOk()
    {
        var dto = new ProfileVersionValidationStatusDto { ProfileVersionId = ProfileSectionTestData.ProfileVersionId };

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileVersionValidationStatusQuery>(query => query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileVersionValidationStatus(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ValidateProfileVersionSection_ReturnsOk_AndForwardsBothIdentifiers()
    {
        var dto = new ProfileSectionValidationResultDto { IsValid = true };

        mediator
            .Setup(sender => sender.Send(
                It.Is<ValidateProfileVersionSectionQuery>(query =>
                    query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId &&
                    query.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().ValidateProfileVersionSection(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ClearProfileVersionSection_ReturnsOk_AndForwardsTheCommand()
    {
        var dto = new ClearProfileSectionResultDto { FieldsClearedCount = 2 };
        var command = new ClearProfileVersionSectionCommand { ProfileVersionId = ProfileSectionTestData.ProfileVersionId, ProfileSectionId = ProfileSectionTestData.SectionId };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().ClearProfileVersionSection(command, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task SaveProfileVersionSection_ReturnsOk_AndForwardsTheCommand()
    {
        var dto = new SaveProfileSectionResultDto { FieldsSavedCount = 3 };
        var answers = new List<ProfileFieldAnswerInputDto> { new() { ProfileFieldId = ProfileSectionTestData.FieldId, TextValue = "Some notes" } };
        var command = new SaveProfileVersionSectionCommand { ProfileVersionId = ProfileSectionTestData.ProfileVersionId, ProfileSectionId = ProfileSectionTestData.SectionId, Answers = answers };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().SaveProfileVersionSection(command, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionSectionContributions_ReturnsOk_AndForwardsBothIdentifiers()
    {
        IReadOnlyList<ProfileSectionContributionDto> dto = [new() { UserName = "jdoe" }];

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileVersionSectionContributionsQuery>(query =>
                    query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId &&
                    query.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileVersionSectionContributions(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileSectionReviewComments_ReturnsOk_AndForwardsBothIdentifiers()
    {
        IReadOnlyList<ProfileReviewCommentDto> dto = [new() { CommentSubject = "A comment" }];

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileSectionReviewCommentsQuery>(query =>
                    query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId &&
                    query.ProfileSectionId == ProfileSectionTestData.SectionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileSectionReviewComments(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionAllRevisionDates_ReturnsOk()
    {
        var dto = new ProfileVersionAllRevisionDatesDto { ProfileVersionId = ProfileSectionTestData.ProfileVersionId };

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileVersionAllRevisionDatesQuery>(query => query.ProfileVersionId == ProfileSectionTestData.ProfileVersionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileVersionAllRevisionDates(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }
}
