using CDC.Api.Features.ProfileSections.Dtos;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ProfileSections.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileSectionHandlerTests
{
    private readonly Mock<IProfileSectionService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetProfileQuestionnaireMetadataQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileQuestionnaireMetadataDto();

        service.Setup(svc => svc.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new GetProfileQuestionnaireMetadataQueryHandler(service.Object)
            .Handle(new GetProfileQuestionnaireMetadataQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileSectionAnswersQueryHandler_ReturnsSuccess_AndForwardsBothIdentifiers()
    {
        var dto = new ProfileSectionAnswersDto
        {
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            ProfileSectionId = ProfileSectionTestData.SectionId
        };

        service
            .Setup(svc => svc.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileSectionAnswersQueryHandler(service.Object)
            .Handle(
                new GetProfileSectionAnswersQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }
}
