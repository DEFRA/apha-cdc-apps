using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.PrioritisationVariables.Commands;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesHandlerTests
{
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IPrioritisationVariablesRepository> repository = new(MockBehavior.Strict);

    [Fact]
    public async Task GetPrioritisationCategoriesQueryHandler_ReturnsCategoriesWithCriteria()
    {
        IReadOnlyList<PrioritisationCategory> categories = [CreateCategory()];
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);

        var result = await new GetPrioritisationCategoriesQueryHandler(repository.Object)
            .Handle(new GetPrioritisationCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var category = result.Value.Should().ContainSingle().Subject;
        category.Id.Should().Be(CategoryId);
        var criterion = category.Criteria.Should().ContainSingle().Subject;
        criterion.Id.Should().Be(CriterionId);
        criterion.Weight.Should().Be(10);
        var value = criterion.Values.Should().ContainSingle().Subject;
        value.Id.Should().Be(ValueId);
        value.Score.Should().Be(5);
    }

    [Fact]
    public async Task UpdateCriterionCommandHandler_UpdatesWeightAndValueScoresWithTheExistingName()
    {
        IReadOnlyList<PrioritisationCategory> categories = [CreateCategory()];
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        repository.Setup(repo => repo.UpdateCriterionAsync(CriterionId, "Impact", 42, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.UpdateCriterionValueScoreAsync(ValueId, 99, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new UpdateCriterionCommandHandler(repository.Object)
            .Handle(new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 99)]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateCriterionAsync(CriterionId, "Impact", 42, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateCriterionValueScoreAsync(ValueId, 99, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCriterionCommandHandler_ReturnsNotFound_WhenCriterionDoesNotExist()
    {
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await new UpdateCriterionCommandHandler(repository.Object)
            .Handle(new UpdateCriterionCommand(CriterionId, 42, []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetRankingRangeQueryHandler_ReturnsTheCurrentRange()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        repository.Setup(repo => repo.GetRankingRangeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PrioritisationRankingRange
        {
            Id = Guid.NewGuid(),
            LowerBound = 15,
            UpperBound = 35,
            RowVersion = rowVersion
        });

        var result = await new GetRankingRangeQueryHandler(repository.Object)
            .Handle(new GetRankingRangeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LowerBound.Should().Be(15);
        result.Value.UpperBound.Should().Be(35);
        result.Value.RowVersion.Should().Be(Convert.ToBase64String(rowVersion));
    }

    [Fact]
    public async Task UpdateRankingRangeCommandHandler_ReturnsTheNewRowVersion()
    {
        var oldRowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var newRowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
        repository.Setup(repo => repo.UpdateRankingRangeAsync(15, 35, oldRowVersion, It.IsAny<CancellationToken>())).ReturnsAsync(newRowVersion);

        var result = await new UpdateRankingRangeCommandHandler(repository.Object, NullLogger<UpdateRankingRangeCommandHandler>.Instance)
            .Handle(new UpdateRankingRangeCommand(15, 35, Convert.ToBase64String(oldRowVersion)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowVersion.Should().Be(Convert.ToBase64String(newRowVersion));
    }

    [Fact]
    public async Task UpdateRankingRangeCommandHandler_ReturnsConflict_WhenRowVersionIsStale()
    {
        var oldRowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        repository.Setup(repo => repo.UpdateRankingRangeAsync(15, 35, oldRowVersion, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("The prioritisation variables have been edited by another user."));

        var result = await new UpdateRankingRangeCommandHandler(repository.Object, NullLogger<UpdateRankingRangeCommandHandler>.Instance)
            .Handle(new UpdateRankingRangeCommand(15, 35, Convert.ToBase64String(oldRowVersion)), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    private static PrioritisationCategory CreateCategory() => new()
    {
        Id = CategoryId,
        Name = "Animal welfare",
        Criteria =
        [
            new PrioritisationCriterion
            {
                Id = CriterionId,
                CategoryId = CategoryId,
                Code = "C1",
                Name = "Impact",
                Weight = 10,
                Values =
                [
                    new PrioritisationCriterionValue
                    {
                        Id = ValueId,
                        CriterionId = CriterionId,
                        Value = "N/A",
                        Score = 5
                    }
                ]
            }
        ]
    };
}
