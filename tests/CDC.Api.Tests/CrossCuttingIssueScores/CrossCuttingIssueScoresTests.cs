using System.Data;
using CDC.Api.Domain.Common;
using CDC.Api.Features.CrossCuttingIssueScores;
using CDC.Api.Features.CrossCuttingIssueScores.Commands;
using CDC.Api.Features.CrossCuttingIssueScores.Dtos;
using CDC.Api.Features.CrossCuttingIssueScores.Interfaces;
using CDC.Api.Features.CrossCuttingIssueScores.Queries;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.CrossCuttingIssueScores;

public sealed class CrossCuttingIssueScoresTests : IDisposable
{
    private const string GetAll = "spgaSpeciesPrioritisationVariables";
    private const string UpdateValue = "spuSpeciesPrioritisationCriterionValue";
    private const string Recalculate = "sppSpeciesPrioritisationScore";

    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherValueId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<ICrossCuttingIssueScoresRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<CrossCuttingIssueScoresRepository>> repositoryLogger = new();

    public CrossCuttingIssueScoresTests() =>
        repositoryLogger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IReadOnlyList<CrossCuttingIssueCategoryDto> CreateCategories() =>
    [
        new(CategoryId, "Animal identification", 1,
        [
            new(CriterionId, "Accuracy", 1,
            [
                new(ValueId, "Unique id", 0, 1),
                new(OtherValueId, "No id", 100, 2)
            ])
        ])
    ];

    private UpdateCrossCuttingIssueScoresCommandHandler CreateHandler() =>
        new(repository.Object, NullLogger<UpdateCrossCuttingIssueScoresCommandHandler>.Instance);

    private CrossCuttingIssueScoresRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), repositoryLogger.Object);

    private CrossCuttingIssueScoresController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetQueryHandler_ReturnsTheCategories()
    {
        var categories = CreateCategories();
        repository.Setup(repo => repo.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);

        var result = await new GetCrossCuttingIssueScoresQueryHandler(repository.Object)
            .Handle(new GetCrossCuttingIssueScoresQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(categories);
    }

    [Fact]
    public async Task UpdateCommandHandler_SavesTheScores()
    {
        repository.Setup(repo => repo.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());
        repository
            .Setup(repo => repo.UpdateScoresAsync(
                It.Is<IReadOnlyDictionary<Guid, int>>(scores => scores.Count == 2 && scores[ValueId] == 7 && scores[OtherValueId] == 100),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(
            new UpdateCrossCuttingIssueScoresCommand([new(ValueId, 7), new(OtherValueId, 100)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateCommandHandler_KeepsTheLastScore_WhenAValueIsRepeated()
    {
        repository.Setup(repo => repo.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());
        repository
            .Setup(repo => repo.UpdateScoresAsync(
                It.Is<IReadOnlyDictionary<Guid, int>>(scores => scores.Count == 1 && scores[ValueId] == 9),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(
            new UpdateCrossCuttingIssueScoresCommand([new(ValueId, 3), new(ValueId, 9)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateCommandHandler_ReturnsValidationFailed_WhenAScoreIsOutOfRange(int score)
    {
        var result = await CreateHandler().Handle(
            new UpdateCrossCuttingIssueScoresCommand([new(ValueId, score)]),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateCommandHandler_ReturnsNotFound_WhenAValueDoesNotExist()
    {
        repository.Setup(repo => repo.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());

        var result = await CreateHandler().Handle(
            new UpdateCrossCuttingIssueScoresCommand([new(Guid.NewGuid(), 5)]),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        repository.Verify(repo => repo.UpdateScoresAsync(It.IsAny<IReadOnlyDictionary<Guid, int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var categories = CreateCategories();
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetCrossCuttingIssueScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(categories));

        var response = await CreateController().GetCategories(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(categories);
    }

    [Fact]
    public async Task UpdateScores_ReturnsNoContent()
    {
        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateCrossCuttingIssueScoresCommand>(command =>
                    command.Scores.Count == 1 && command.Scores[0].ValueId == ValueId && command.Scores[0].Score == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().UpdateScores(
            new UpdateCrossCuttingIssueScoresRequestDto([new(ValueId, 7)]),
            CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Theory]
    [InlineData(ResultStatus.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    public async Task UpdateScores_ReturnsProblem_WhenTheCommandFails(ResultStatus status, int expectedStatusCode)
    {
        var failure = status == ResultStatus.NotFound
            ? Result.NotFound<Unit>("missing")
            : Result.ValidationFailed<Unit>("invalid");
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateCrossCuttingIssueScoresCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var response = await CreateController().UpdateScores(
            new UpdateCrossCuttingIssueScoresRequestDto([new(ValueId, 7)]),
            CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task GetCategoriesAsync_MapsAllFourResultSetsInSequence()
    {
        connection.Script(GetAll, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("LastUpdated"),
                new FakeResultSet(["Id", "Name"], [[CategoryId, "Animal identification"]]),
                new FakeResultSet(
                    ["Id", "SpeciesPrioritisationCategoryId", "SpeciesFieldId", "Name"],
                    [[CriterionId, CategoryId, Guid.NewGuid(), "Accuracy"]]),
                new FakeResultSet(
                    ["Id", "SpeciesPrioritisationCriterionId", "LookupValue", "Score"],
                    [[ValueId, CriterionId, "Unique id", 0], [OtherValueId, CriterionId, "No id", 100]])
            ]
        });

        var categories = await CreateRepository().GetCategoriesAsync(CancellationToken.None);

        var category = categories.Should().ContainSingle().Subject;
        category.Id.Should().Be(CategoryId);
        category.SequenceNumber.Should().Be(1);
        var criterion = category.Criteria.Should().ContainSingle().Subject;
        criterion.Name.Should().Be("Accuracy");
        criterion.Values.Select(value => (value.Id, value.Score, value.SequenceNumber))
            .Should().Equal((ValueId, 0, 1), (OtherValueId, 100, 2));
    }

    [Fact]
    public async Task GetCategoriesAsync_Rethrows_OnDbException()
    {
        connection.Script(GetAll, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetCategoriesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateScoresAsync_UpdatesEachValueThenRecalculates_InOneCommittedTransaction()
    {
        connection.Script(UpdateValue, new FakeCommandScript());
        connection.Script(Recalculate, new FakeCommandScript());

        await CreateRepository().UpdateScoresAsync(
            new Dictionary<Guid, int> { [ValueId] = 7, [OtherValueId] = 12 },
            CancellationToken.None);

        connection.Executed.Select(command => command.CommandText).Should().Equal(UpdateValue, UpdateValue, Recalculate);
        connection.Executed.Should().OnlyContain(command => command.CommandType == CommandType.StoredProcedure && command.HadTransaction);
        connection.Executed[0].Parameters["Id"].Should().Be(ValueId);
        connection.Executed[0].Parameters["Score"].Should().Be(7);
        connection.Executed[1].Parameters["Id"].Should().Be(OtherValueId);
        connection.Executed[1].Parameters["Score"].Should().Be(12);
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.Committed.Should().BeTrue();
        transaction.RolledBack.Should().BeFalse();
    }

    [Theory]
    [InlineData(UpdateValue)]
    [InlineData(Recalculate)]
    public async Task UpdateScoresAsync_RollsBackAndRethrows_WhenAStepFails(string failingProcedure)
    {
        connection.Script(UpdateValue, new FakeCommandScript());
        connection.Script(Recalculate, new FakeCommandScript());
        connection.Script(failingProcedure, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateScoresAsync(new Dictionary<Guid, int> { [ValueId] = 7 }, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.RolledBack.Should().BeTrue();
        transaction.Committed.Should().BeFalse();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);

    /// <summary>Minimal factory so <c>ControllerBase.Problem()</c> works without the MVC pipeline.</summary>
    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new()
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }
}
