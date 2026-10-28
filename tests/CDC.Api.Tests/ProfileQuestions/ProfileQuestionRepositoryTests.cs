using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileQuestionRepository>> logger = new();

    public ProfileQuestionRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileQuestionRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetProfileQuestionAsync_ReturnsNull_WhenQuestionDoesNotExist()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id", "Name")]
        });

        var question = await CreateRepository().GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        question.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileQuestionAsync_MapsRow()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "QuestionNumber", "UserGuidance", "ProfileSectionId", "NonTechnicalName", "LastUpdated"],
                    [[
                        ProfileQuestionTestData.QuestionId, "Is it endemic?", "Endemic", 1, "Consider surveillance data.",
                        Guid.Empty, "Is the disease present?", ProfileQuestionTestData.RowVersion
                    ]])
            ]
        });

        var question = await CreateRepository().GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        question.Should().NotBeNull();
        question!.Name.Should().Be("Is it endemic?");
        question.ShortName.Should().Be("Endemic");
        question.NonTechnicalName.Should().Be("Is the disease present?");
        question.QuestionNumber.Should().Be(1);
        question.LastUpdated.Should().Equal(ProfileQuestionTestData.RowVersion);
    }

    [Fact]
    public async Task GetProfileQuestionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileQuestionAsync(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileQuestionInfoListAsync_MapsRows()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestionBySectionId, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "Name", "QuestionNumber"], [[ProfileQuestionTestData.QuestionId, "Q1", 1]])]
        });

        var questions = await CreateRepository().GetProfileQuestionInfoListAsync(ProfileQuestionTestData.ProfileSectionId, CancellationToken.None);

        questions.Should().ContainSingle();
        questions[0].Name.Should().Be("Q1");
        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("ProfileSectionId")
            .WhoseValue.Should().Be(ProfileQuestionTestData.ProfileSectionId);
    }

    [Fact]
    public async Task GetProfileQuestionInfoListAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestionBySectionId, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileQuestionInfoListAsync(ProfileQuestionTestData.ProfileSectionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileQuestionsAsync_ReadsEachQuestionOnOneConnection_SkippingAnyThatDoNotExist()
    {
        var secondId = Guid.NewGuid();
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "QuestionNumber", "UserGuidance", "ProfileSectionId", "NonTechnicalName", "LastUpdated"],
                    [[ProfileQuestionTestData.QuestionId, "Is it endemic?", "Endemic", 1, "Guidance", Guid.Empty, "Plain", ProfileQuestionTestData.RowVersion]])
            ]
        });

        var questions = await CreateRepository().GetProfileQuestionsAsync([ProfileQuestionTestData.QuestionId, secondId], CancellationToken.None);

        questions.Should().HaveCount(2);
        connection.Executed.Should().HaveCount(2);
        connection.Executed.Select(command => command.Parameters["Id"]).Should().Equal(ProfileQuestionTestData.QuestionId, secondId);
    }

    [Fact]
    public async Task GetProfileQuestionsAsync_ReturnsNothing_WhenNoQuestionExists()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript { ResultSets = [FakeResultSet.Empty("Id", "Name")] });

        var questions = await CreateRepository().GetProfileQuestionsAsync([ProfileQuestionTestData.QuestionId], CancellationToken.None);

        questions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileQuestionsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileQuestionsAsync([ProfileQuestionTestData.QuestionId], CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetPrioritisationGuidanceAsync_GroupsCriteriaUnderTheirCategories_ListingEachCodeOnce()
    {
        var welfareId = Guid.NewGuid();
        var healthId = Guid.NewGuid();
        var unshownId = Guid.NewGuid();
        var firstQuestion = Guid.NewGuid();
        var secondQuestion = Guid.NewGuid();
        var thirdQuestion = Guid.NewGuid();
        var fourthQuestion = Guid.NewGuid();
        string[] criterionColumns = ["ProfileQuestionId", "PrioritisationCategoryId", "SectionNumber", "QuestionNumber", "QuestionName", "IsPerSpecies", "CriterionCode", "CriterionName"];

        connection.Script(ProfileQuestionStoredProcedures.GetPrioritisationGuidanceData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["PrioritisationCategoryId", "DisplayName", "Name"],
                    [[welfareId, "Welfare", "WEL"], [healthId, "Public health", "PH"]]),
                new FakeResultSet(
                    criterionColumns,
                    [
                        [firstQuestion, welfareId, 3, 20, "Q20", false, "WEL01", "Suffering"],
                        [secondQuestion, healthId, 6, 14, "Q14", false, "PH01", "Zoonosis"],
                        // The same criterion mapped to a second question is listed once, from the first reading.
                        [thirdQuestion, healthId, 6, 15, "Q15", false, "WEL01", "Suffering again"],
                        // A category the report does not show is dropped, but its code is still taken.
                        [fourthQuestion, unshownId, 7, 1, "Q1", false, "XX01", "Hidden"],
                        [Guid.NewGuid(), welfareId, 7, 2, "Q2", false, "XX01", "Hidden again"]
                    ])
            ]
        });

        var categories = await CreateRepository().GetPrioritisationGuidanceAsync(CancellationToken.None);

        categories.Select(category => category.DisplayName).Should().Equal("Welfare", "Public health");
        categories[0].CategoryName.Should().Be("WEL");
        var welfare = categories[0].Criteria.Should().ContainSingle().Subject;
        welfare.ProfileQuestionId.Should().Be(firstQuestion);
        welfare.CriterionCode.Should().Be("WEL01");
        welfare.CriterionName.Should().Be("Suffering");
        var health = categories[1].Criteria.Should().ContainSingle().Subject;
        health.CriterionCode.Should().Be("PH01");
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetPrioritisationGuidanceAsync_ReturnsCategoriesWithNoCriteria_WhenThereIsNoSecondResultSet()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetPrioritisationGuidanceData, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["PrioritisationCategoryId", "DisplayName", "Name"], [[Guid.NewGuid(), "Welfare", "WEL"]])]
        });

        var categories = await CreateRepository().GetPrioritisationGuidanceAsync(CancellationToken.None);

        categories.Should().ContainSingle().Which.Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrioritisationGuidanceAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetPrioritisationGuidanceData, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetPrioritisationGuidanceAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateProfileQuestionAsync_ReturnsNull_WhenNoRowUpdated()
    {
        connection.Script(ProfileQuestionStoredProcedures.UpdateProfileQuestion, new FakeCommandScript());

        var command = new UpdateProfileQuestionCommand { Id = ProfileQuestionTestData.QuestionId, LastUpdated = ProfileQuestionTestData.RowVersion };

        var result = await CreateRepository().UpdateProfileQuestionAsync(command, CancellationToken.None);

        result.Should().BeNull();
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileQuestionAsync_UpdatesThenRereadsTheRow()
    {
        connection.Script(ProfileQuestionStoredProcedures.UpdateProfileQuestion, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = ProfileQuestionTestData.NewRowVersion }
        });
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "QuestionNumber", "UserGuidance", "ProfileSectionId", "NonTechnicalName", "LastUpdated"],
                    [[
                        ProfileQuestionTestData.QuestionId, "New name", "Endemic", 1, "New guidance",
                        Guid.Empty, "New non-technical name", ProfileQuestionTestData.NewRowVersion
                    ]])
            ]
        });

        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "New name",
            NonTechnicalName = "New non-technical name",
            UserGuidance = "New guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        var result = await CreateRepository().UpdateProfileQuestionAsync(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New name");
        result.LastUpdated.Should().Equal(ProfileQuestionTestData.NewRowVersion);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileQuestionAsync_ThrowsConcurrencyException_OnRowVersionMismatch()
    {
        connection.Script(ProfileQuestionStoredProcedures.UpdateProfileQuestion, new FakeCommandScript
        {
            Throws = new FakeDbException("The question has been edited by another user.")
        });

        var command = new UpdateProfileQuestionCommand { Id = ProfileQuestionTestData.QuestionId, LastUpdated = ProfileQuestionTestData.RowVersion };

        var act = () => CreateRepository().UpdateProfileQuestionAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain(ProfileQuestionTestData.QuestionId.ToString());
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
