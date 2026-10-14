using System.Data;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileReports.Queries;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportGuidanceRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileReportContentRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<ProfileReportContentRepository>.Instance);

    [Fact]
    public async Task GetQuestionGuidanceAsync_ReadsTheGuidanceOfEachQuestion_FromSpgProfileQuestion()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "QuestionNumber", "UserGuidance", "LastUpdated", "NonTechnicalName"],
                    [[first, "Is it present?", "Present", 1, "<p>Guidance text</p>", new byte[8], "Is it here?"]])
            ]
        });

        var guidance = await CreateRepository().GetQuestionGuidanceAsync([first, second], CancellationToken.None);

        guidance.Select(item => item.QuestionId).Should().Equal(first, second);
        guidance.Should().OnlyContain(item => item.UserGuidance == "<p>Guidance text</p>");
        connection.Executed.Select(command => command.Parameters["Id"]).Should().Equal(first, second);
        connection.Executed.Should().OnlyContain(command => command.CommandType == CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetQuestionGuidanceAsync_SkipsAQuestionThatDoesNotExist_AndTreatsNullGuidanceAsEmpty()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "QuestionNumber", "UserGuidance", "LastUpdated", "NonTechnicalName"],
                    [[Guid.NewGuid(), "Q", "Q", 1, null, new byte[8], null]])
            ]
        });
        var repository = CreateRepository();

        var withNullGuidance = await repository.GetQuestionGuidanceAsync([Guid.NewGuid()], CancellationToken.None);
        var none = await repository.GetQuestionGuidanceAsync([], CancellationToken.None);

        withNullGuidance.Should().ContainSingle().Which.UserGuidance.Should().BeEmpty();
        none.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQuestionGuidanceAsync_ReturnsNothing_WhenTheProcedureFindsNoQuestion()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript { ResultSets = [FakeResultSet.Empty("Id")] });

        var guidance = await CreateRepository().GetQuestionGuidanceAsync([Guid.NewGuid()], CancellationToken.None);

        guidance.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQuestionGuidanceAsync_RethrowsAndLogs_WhenTheProcedureFails()
    {
        connection.Script(ProfileQuestionStoredProcedures.GetProfileQuestion, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetQuestionGuidanceAsync([Guid.NewGuid()], CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}

public class ProfileReportGuidanceFeatureTests
{
    private readonly Mock<IProfileReportContentRepository> contentRepository = new(MockBehavior.Strict);
    private readonly Mock<IProfileReportContentService> contentService = new(MockBehavior.Strict);

    [Fact]
    public async Task Service_AsksTheRepositoryForEachDistinctQuestionOnce()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        IReadOnlyList<ProfileQuestionGuidanceDto> found = [new() { QuestionId = first, UserGuidance = "g" }];
        contentRepository
            .Setup(repo => repo.GetQuestionGuidanceAsync(It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { first, second })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(found);
        var service = new ProfileReportContentService(Mock.Of<IProfileReportRepository>(), contentRepository.Object, NullLogger<ProfileReportContentService>.Instance);

        var result = await service.GetQuestionGuidanceAsync([first, second, first], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(found);
        contentRepository.VerifyAll();
    }

    [Fact]
    public void Validator_AcceptsAnyNumberOfQuestionsUpToTheLimit_AndRejectsEmptyIdsAndTooMany()
    {
        var validator = new GetProfileQuestionGuidanceQueryValidator();

        validator.Validate(new GetProfileQuestionGuidanceQuery([])).IsValid.Should().BeTrue();
        validator.Validate(new GetProfileQuestionGuidanceQuery([Guid.NewGuid()])).IsValid.Should().BeTrue();
        validator.Validate(new GetProfileQuestionGuidanceQuery([Guid.Empty])).IsValid.Should().BeFalse();
        validator.Validate(new GetProfileQuestionGuidanceQuery(null!)).IsValid.Should().BeFalse();
        validator.Validate(new GetProfileQuestionGuidanceQuery([.. Enumerable.Range(0, GetProfileQuestionGuidanceQueryValidator.MaxQuestions + 1).Select(_ => Guid.NewGuid())])).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_ReturnsTheServiceResult()
    {
        var ids = new[] { Guid.NewGuid() };
        var expected = Result.Success<IReadOnlyList<ProfileQuestionGuidanceDto>>([]);
        contentService.Setup(svc => svc.GetQuestionGuidanceAsync(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new GetProfileQuestionGuidanceQueryHandler(contentService.Object).Handle(new GetProfileQuestionGuidanceQuery(ids), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Controller_ReturnsTheGuidance()
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        IReadOnlyList<ProfileQuestionGuidanceDto> guidance = [new() { QuestionId = Guid.NewGuid(), UserGuidance = "g" }];
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileQuestionGuidanceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(guidance));
        var controller = new ProfileReportsController(mediator.Object)
        {
            ProblemDetailsFactory = Mock.Of<ProblemDetailsFactory>(),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.GetQuestionGuidance(new ProfileQuestionGuidanceRequest { QuestionIds = [Guid.NewGuid()] }, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(guidance);
    }
}
