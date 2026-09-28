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
