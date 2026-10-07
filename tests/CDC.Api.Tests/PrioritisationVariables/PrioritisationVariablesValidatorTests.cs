using CDC.Api.Features.PrioritisationVariables.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesValidatorTests
{
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void UpdateCriterionCommandValidator_ValidCommand_Passes()
    {
        var result = new UpdateCriterionCommandValidator().Validate(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 5)]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateCriterionCommandValidator_EmptyCriterionId_Fails()
    {
        var result = new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(Guid.Empty, 42, []));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void UpdateCriterionCommandValidator_WeightOutOfRange_Fails(int weight)
    {
        var result = new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(CriterionId, weight, []));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCriterionCommandValidator_ValueScoreWithEmptyId_Fails()
    {
        var result = new UpdateCriterionCommandValidator().Validate(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(Guid.Empty, 5)]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCriterionCommandValidator_ValueScoreOutOfRange_Fails()
    {
        var result = new UpdateCriterionCommandValidator().Validate(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 1000)]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_ValidCommand_Passes()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(
            new UpdateRankingRangeCommand(10, 90, Convert.ToBase64String(new byte[8])));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public void UpdateRankingRangeCommandValidator_LowerBoundOutOfRange_Fails(int lowerBound)
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(
            new UpdateRankingRangeCommand(lowerBound, 999, Convert.ToBase64String(new byte[8])));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void UpdateRankingRangeCommandValidator_UpperBoundOutOfRange_Fails(int upperBound)
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(
            new UpdateRankingRangeCommand(0, upperBound, Convert.ToBase64String(new byte[8])));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_LowerBoundNotLessThanUpperBound_Fails()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(
            new UpdateRankingRangeCommand(50, 50, Convert.ToBase64String(new byte[8])));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_EmptyRowVersion_Fails()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(new UpdateRankingRangeCommand(10, 90, string.Empty));

        result.IsValid.Should().BeFalse();
    }
}
