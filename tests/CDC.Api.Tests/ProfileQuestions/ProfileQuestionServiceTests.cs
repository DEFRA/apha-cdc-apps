using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionServiceTests
{
    private readonly Mock<IProfileQuestionRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileSectionRepository> sectionRepository = new(MockBehavior.Strict);

    private CDC.Api.Features.ProfileQuestions.ProfileQuestionService CreateService() =>
        new(repository.Object, sectionRepository.Object, NullLogger<CDC.Api.Features.ProfileQuestions.ProfileQuestionService>.Instance);

    [Fact]
    public async Task GetProfileGuidanceContent_ListsSectionsAndQuestionsInOrder_WithTheirTitlesAndGuidance()
    {
        var firstSectionId = Guid.NewGuid();
        var secondSectionId = Guid.NewGuid();
        var questionOne = Guid.NewGuid();
        var questionTwo = Guid.NewGuid();
        var questionThree = Guid.NewGuid();
        var missingQuestion = Guid.NewGuid();

        sectionRepository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileQuestionnaireMetadata
            {
                // Out of order, to show the service sorts them.
                Sections =
                [
                    Section(secondSectionId, 2, "Epidemiology", Metadata(questionThree, secondSectionId, 1)),
                    Section(firstSectionId, 1, "Summary", Metadata(questionTwo, firstSectionId, 2), Metadata(missingQuestion, firstSectionId, 3), Metadata(questionOne, firstSectionId, 1))
                ]
            });
        repository
            .Setup(repo => repo.GetProfileQuestionsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 4), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Question(questionOne, "One"), Question(questionTwo, "Two"), Question(questionThree, "Three")]);
        repository
            .Setup(repo => repo.GetPrioritisationGuidanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PrioritisationGuidanceCategory
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Welfare",
                    CategoryName = "WEL",
                    Criteria = [new PrioritisationGuidanceCriterion { ProfileQuestionId = questionTwo, CriterionCode = "WEL01", CriterionName = "Suffering" }]
                }
            ]);

        var content = await CreateService().GetProfileGuidanceContentAsync(CancellationToken.None);

        content.Sections.Select(section => section.SectionNumber).Should().Equal(1, 2);
        content.Sections[0].Name.Should().Be("Summary");
        content.Sections[0].Questions.Select(question => question.Id).Should().Equal(questionOne, questionTwo);
        content.Sections[0].Questions[0].Name.Should().Be("One");
        content.Sections[0].Questions[0].NonTechnicalName.Should().Be("One plain");
        content.Sections[0].Questions[0].UserGuidance.Should().Be("One guidance");
        content.Sections[1].Questions.Should().ContainSingle().Which.Id.Should().Be(questionThree);
        var category = content.PrioritisationCategories.Should().ContainSingle().Subject;
        category.DisplayName.Should().Be("Welfare");
        var criterion = category.Criteria.Should().ContainSingle().Subject;
        criterion.ProfileQuestionId.Should().Be(questionTwo);
        criterion.CriterionCode.Should().Be("WEL01");
        criterion.CriterionName.Should().Be("Suffering");
    }

    private static ProfileSectionMetadata Section(Guid id, int number, string name, params ProfileQuestionMetadata[] questions) => new()
    {
        Id = id,
        Name = name,
        ShortName = name,
        SectionNumber = number,
        Questions = questions
    };

    private static ProfileQuestionMetadata Metadata(Guid id, Guid sectionId, int number) => new()
    {
        Id = id,
        SectionId = sectionId,
        ShortName = $"Q{number}",
        QuestionNumber = number,
        IsPerSpecies = false,
        IsRepeating = false,
        Fields = []
    };

    private static ProfileQuestion Question(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        ShortName = name,
        NonTechnicalName = name + " plain",
        QuestionNumber = 99,
        UserGuidance = name + " guidance",
        LastUpdated = ProfileQuestionTestData.RowVersion
    };

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
