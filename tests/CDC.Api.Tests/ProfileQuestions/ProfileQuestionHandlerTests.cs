using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileQuestions.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionHandlerTests
{
    private readonly Mock<IProfileQuestionService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetProfileQuestionQueryHandler_ReturnsSuccess()
    {
        var dto = ProfileQuestionTestData.ProfileQuestionDto();

        service.Setup(svc => svc.GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new GetProfileQuestionQueryHandler(service.Object)
            .Handle(new GetProfileQuestionQuery(ProfileQuestionTestData.QuestionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileQuestionQueryHandler_ReturnsNotFound_WhenServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileQuestionDto?)null);

        var result = await new GetProfileQuestionQueryHandler(service.Object)
            .Handle(new GetProfileQuestionQuery(ProfileQuestionTestData.QuestionId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileQuestionInfoListQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileQuestionInfoDto> questions = [new ProfileQuestionInfoDto { Id = ProfileQuestionTestData.QuestionId }];

        service
            .Setup(svc => svc.GetProfileQuestionInfoListAsync(ProfileQuestionTestData.ProfileSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions);

        var result = await new GetProfileQuestionInfoListQueryHandler(service.Object)
            .Handle(new GetProfileQuestionInfoListQuery(ProfileQuestionTestData.ProfileSectionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(questions);
    }

    [Fact]
    public async Task GetProfileGuidanceReportQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileGuidanceReportDto { ReportType = ProfileGuidanceReportType.All, IsAvailable = false };

        service
            .Setup(svc => svc.GetProfileGuidanceReportAsync(ProfileGuidanceReportType.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileGuidanceReportQueryHandler(service.Object)
            .Handle(new GetProfileGuidanceReportQuery(ProfileGuidanceReportType.All), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateProfileQuestionCommandHandler_ReturnsSuccess()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };
        var dto = ProfileQuestionTestData.ProfileQuestionDto();

        service.Setup(svc => svc.UpdateProfileQuestionAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await CreateUpdateHandler(isProfileEditor: true)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateProfileQuestionCommandHandler_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        service.Setup(svc => svc.UpdateProfileQuestionAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync((ProfileQuestionDto?)null);

        var result = await CreateUpdateHandler(isProfileEditor: true)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateProfileQuestionCommandHandler_ReturnsConflict_OnConcurrencyException()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        service
            .Setup(svc => svc.UpdateProfileQuestionAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await CreateUpdateHandler(isProfileEditor: true)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task UpdateProfileQuestionCommandHandler_ReturnsForbidden_WhenUserCannotEditGuidance(bool isProfileEditor, bool isUserManagementSystem)
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        // The strict service mock has no setup, so any call to it would fail the test.
        var result = await CreateUpdateHandler(isProfileEditor, isUserManagementSystem)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task GetProfileGuidanceContentQueryHandler_ReturnsTheContent_ForAProfileEditor()
    {
        var content = new CDC.Common.Contracts.ProfileGuidanceContentDto();
        service.Setup(svc => svc.GetProfileGuidanceContentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var result = await CreateContentHandler(isProfileEditor: true).Handle(new GetProfileGuidanceContentQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(content);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GetProfileGuidanceContentQueryHandler_ReturnsForbidden_WhenUserCannotEditGuidance(bool isProfileEditor, bool isUserManagementSystem)
    {
        // The strict service mock has no setup, so any call to it would fail the test.
        var result = await CreateContentHandler(isProfileEditor, isUserManagementSystem).Handle(new GetProfileGuidanceContentQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    private GetProfileGuidanceContentQueryHandler CreateContentHandler(bool isProfileEditor, bool isUserManagementSystem = false)
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isProfileEditor);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagementSystem);

        return new GetProfileGuidanceContentQueryHandler(service.Object, userContext.Object, NullLogger<GetProfileGuidanceContentQueryHandler>.Instance);
    }

    private UpdateProfileQuestionCommandHandler CreateUpdateHandler(bool isProfileEditor, bool isUserManagementSystem = false)
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isProfileEditor);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagementSystem);

        return new UpdateProfileQuestionCommandHandler(service.Object, userContext.Object, NullLogger<UpdateProfileQuestionCommandHandler>.Instance);
    }
}
