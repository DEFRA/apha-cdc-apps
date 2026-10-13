using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ProfileSections.Queries;
using CDC.Common.Contracts;
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

    [Fact]
    public async Task GetProfileVersionValidationStatusQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileVersionValidationStatusDto { ProfileVersionId = ProfileSectionTestData.ProfileVersionId };

        service
            .Setup(svc => svc.GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileVersionValidationStatusQueryHandler(service.Object)
            .Handle(new GetProfileVersionValidationStatusQuery(ProfileSectionTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ValidateProfileVersionSectionQueryHandler_ReturnsSuccess_AndForwardsBothIdentifiers()
    {
        var dto = new ProfileSectionValidationResultDto { IsValid = true };

        service
            .Setup(svc => svc.ValidateProfileVersionSectionAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new ValidateProfileVersionSectionQueryHandler(service.Object)
            .Handle(
                new ValidateProfileVersionSectionQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task ClearProfileVersionSectionCommandHandler_ReturnsSuccess_AndForwardsBothIdentifiers()
    {
        var dto = new ClearProfileSectionResultDto { FieldsClearedCount = 4 };

        service
            .Setup(svc => svc.ClearProfileVersionSectionAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new ClearProfileVersionSectionCommandHandler(service.Object)
            .Handle(
                new ClearProfileVersionSectionCommand { ProfileVersionId = ProfileSectionTestData.ProfileVersionId, ProfileSectionId = ProfileSectionTestData.SectionId },
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task SaveProfileVersionSectionCommandHandler_ReturnsSuccess_AndForwardsTheAnswers()
    {
        var dto = new SaveProfileSectionResultDto { FieldsSavedCount = 3 };
        var answers = new List<ProfileFieldAnswerInputDto> { new() { ProfileFieldId = ProfileSectionTestData.FieldId, TextValue = "Some notes" } };

        service
            .Setup(svc => svc.SaveProfileVersionSectionAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                answers,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new SaveProfileVersionSectionCommandHandler(service.Object)
            .Handle(
                new SaveProfileVersionSectionCommand { ProfileVersionId = ProfileSectionTestData.ProfileVersionId, ProfileSectionId = ProfileSectionTestData.SectionId, Answers = answers },
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileSectionContributionDto> dto = [new() { UserName = "jdoe" }];

        service
            .Setup(svc => svc.GetProfileVersionSectionContributionsAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileVersionSectionContributionsQueryHandler(service.Object)
            .Handle(
                new GetProfileVersionSectionContributionsQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileReviewCommentDto> dto = [new() { CommentSubject = "A comment" }];

        var reviewCommentService = new Mock<IProfileReviewCommentService>(MockBehavior.Strict);
        reviewCommentService
            .Setup(svc => svc.GetAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileSectionReviewCommentsQueryHandler(reviewCommentService.Object)
            .Handle(
                new GetProfileSectionReviewCommentsQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionAllRevisionDatesQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileVersionAllRevisionDatesDto { ProfileVersionId = ProfileSectionTestData.ProfileVersionId };

        service
            .Setup(svc => svc.GetProfileVersionAllRevisionDatesAsync(ProfileSectionTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileVersionAllRevisionDatesQueryHandler(service.Object)
            .Handle(new GetProfileVersionAllRevisionDatesQuery(ProfileSectionTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }
}
