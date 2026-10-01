using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.PrioritisationVariables.Commands;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesHandlerTests
{
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IPrioritisationVariablesRepository> repository = new(MockBehavior.Strict);

    private static IReadOnlyList<PrioritisationCategory> CreateCategories() =>
    [
        new PrioritisationCategory
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
                    Values = [new PrioritisationCriterionValue { Id = ValueId, CriterionId = CriterionId, Value = "N/A", Score = 5 }]
                }
            ]
        }
    ];

    [Fact]
    public async Task GetPrioritisationCategoriesQueryHandler_MapsEntitiesToDtos()
    {
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());

        var result = await new GetPrioritisationCategoriesQueryHandler(repository.Object)
            .Handle(new GetPrioritisationCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("Animal welfare");
    }

    [Fact]
    public async Task GetRankingRangeQueryHandler_MapsEntityToDto()
    {
        repository
            .Setup(repo => repo.GetRankingRangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrioritisationRankingRange { Id = Guid.NewGuid(), LowerBound = 10, UpperBound = 90, RowVersion = [1, 2, 3, 4, 5, 6, 7, 8] });

        var result = await new GetRankingRangeQueryHandler(repository.Object)
            .Handle(new GetRankingRangeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LowerBound.Should().Be(10);
        result.Value.UpperBound.Should().Be(90);
    }

    [Fact]
    public async Task UpdateCriterionCommandHandler_ReturnsNotFound_WhenCriterionDoesNotExist()
    {
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());

        var result = await new UpdateCriterionCommandHandler(repository.Object).Handle(
            new UpdateCriterionCommand(Guid.NewGuid(), 42, []),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateCriterionCommandHandler_SavesWeightingAndValueScores()
    {
        repository.Setup(repo => repo.GetCategoriesWithCriteriaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateCategories());
        repository
            .Setup(repo => repo.UpdateCriterionAsync(CriterionId, "Impact", 42, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(repo => repo.UpdateCriterionValueScoreAsync(ValueId, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new UpdateCriterionCommandHandler(repository.Object).Handle(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 7)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateCriterionAsync(CriterionId, "Impact", 42, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateCriterionValueScoreAsync(ValueId, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRankingRangeCommandHandler_ReturnsSuccess()
    {
        byte[] newRowVersion = [9, 9, 9, 9, 9, 9, 9, 9];
        repository
            .Setup(repo => repo.UpdateRankingRangeAsync(10, 90, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newRowVersion);

        var result = await new UpdateRankingRangeCommandHandler(repository.Object, new Moq.Mock<Microsoft.Extensions.Logging.ILogger<UpdateRankingRangeCommandHandler>>().Object)
            .Handle(new UpdateRankingRangeCommand(10, 90, Convert.ToBase64String(new byte[8])), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RowVersion.Should().Be(Convert.ToBase64String(newRowVersion));
    }

    [Fact]
    public async Task UpdateRankingRangeCommandHandler_ReturnsConflict_OnConcurrencyException()
    {
        repository
            .Setup(repo => repo.UpdateRankingRangeAsync(10, 90, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Someone else has updated the prioritisation variables."));

        var result = await new UpdateRankingRangeCommandHandler(repository.Object, new Moq.Mock<Microsoft.Extensions.Logging.ILogger<UpdateRankingRangeCommandHandler>>().Object)
            .Handle(new UpdateRankingRangeCommand(10, 90, Convert.ToBase64String(new byte[8])), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }
}
