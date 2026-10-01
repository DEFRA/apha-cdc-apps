using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Dtos;
using CDC.Api.Features.ProfileSections.Queries;
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
}
