using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionServiceTests
{
    private readonly Mock<IProfileQuestionRepository> repository = new(MockBehavior.Strict);

    private CDC.Api.Features.ProfileQuestions.ProfileQuestionService CreateService() =>
        new(repository.Object, NullLogger<CDC.Api.Features.ProfileQuestions.ProfileQuestionService>.Instance);

    [Fact]
    public async Task GetProfileQuestion_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileQuestion?)null);

        var result = await CreateService().GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileQuestion_MapsQuestionToDto()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileQuestionTestData.ProfileQuestion());

        var result = await CreateService().GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Is it endemic?");
        result.ShortName.Should().Be("Endemic");
    }

    [Fact]
    public async Task GetProfileQuestionInfoList_MapsEachQuestion()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionInfoListAsync(ProfileQuestionTestData.ProfileSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProfileQuestionInfo { Id = ProfileQuestionTestData.QuestionId, Name = "Q1", QuestionNumber = 1 }]);

        var result = await CreateService().GetProfileQuestionInfoListAsync(ProfileQuestionTestData.ProfileSectionId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Q1");
    }

    [Theory]
    [InlineData(ProfileGuidanceReportType.All)]
    [InlineData(ProfileGuidanceReportType.SummaryProfile)]
    [InlineData(ProfileGuidanceReportType.SummaryPrioritisationReport)]
    [InlineData(ProfileGuidanceReportType.QaGuidanceReport)]
    public async Task GetProfileGuidanceReport_ReturnsUnavailableDescriptor_ForEveryReportType(ProfileGuidanceReportType reportType)
    {
        var result = await CreateService().GetProfileGuidanceReportAsync(reportType, CancellationToken.None);

        result.ReportType.Should().Be(reportType);
        result.IsAvailable.Should().BeFalse();
        result.Title.Should().NotBeEmpty();
        result.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateProfileQuestion_ReturnsNull_WhenRepositoryReturnsNull()
    {
        var command = new UpdateProfileQuestionCommand { Id = ProfileQuestionTestData.QuestionId, LastUpdated = ProfileQuestionTestData.RowVersion };

        repository
            .Setup(repo => repo.UpdateProfileQuestionAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileQuestion?)null);

        var result = await CreateService().UpdateProfileQuestionAsync(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileQuestion_MapsUpdatedQuestion()
    {
        var command = new UpdateProfileQuestionCommand { Id = ProfileQuestionTestData.QuestionId, LastUpdated = ProfileQuestionTestData.RowVersion };

        repository
            .Setup(repo => repo.UpdateProfileQuestionAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileQuestionTestData.ProfileQuestion());

        var result = await CreateService().UpdateProfileQuestionAsync(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ProfileQuestionTestData.QuestionId);
    }
}
