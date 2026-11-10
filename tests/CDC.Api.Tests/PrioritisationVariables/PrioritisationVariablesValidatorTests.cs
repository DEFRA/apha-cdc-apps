using CDC.Api.Features.PrioritisationVariables.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesValidatorTests
{
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void UpdateCriterionCommandValidator_RequiresCriterionId()
    {
        var result = new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(Guid.Empty, 42, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(UpdateCriterionCommand.CriterionId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void UpdateCriterionCommandValidator_RejectsWeightOutOfRange(int weight)
    {
        var result = new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(CriterionId, weight, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(UpdateCriterionCommand.Weight));
    }

    [Fact]
    public void UpdateCriterionCommandValidator_RejectsValueScoreOutOfRange()
    {
        var result = new UpdateCriterionCommandValidator().Validate(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 1000)]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCriterionCommandValidator_Passes_ForValidCommand()
    {
        var result = new UpdateCriterionCommandValidator().Validate(
            new UpdateCriterionCommand(CriterionId, 42, [new CriterionValueScore(ValueId, 5)]));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<em> </em>")]
    public void UpdateCriterionCommandValidator_RejectsBlankName(string name)
    {
        var result = new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(CriterionId, 42, [], name));

        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You must enter criterion text.");
    }

    [Fact]
    public void UpdateCriterionCommandValidator_CountsNameLengthWithoutFormatting()
    {
        var atLimit = "<em>" + new string('a', 255) + "</em>";
        var overLimit = new string('a', 256);

        new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(CriterionId, 42, [], atLimit)).IsValid.Should().BeTrue();
        new UpdateCriterionCommandValidator().Validate(new UpdateCriterionCommand(CriterionId, 42, [], overLimit)).Errors
            .Should().Contain(failure => failure.ErrorMessage == "The criterion text must be no longer than 255 characters.");
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_RejectsLowerBoundNotLessThanUpperBound()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(new UpdateRankingRangeCommand(50, 50, "AQIDBAUGBwg="));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(UpdateRankingRangeCommand.LowerBound));
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_RequiresRowVersion()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(new UpdateRankingRangeCommand(15, 35, string.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(UpdateRankingRangeCommand.RowVersion));
    }

    [Fact]
    public void UpdateRankingRangeCommandValidator_Passes_ForValidCommand()
    {
        var result = new UpdateRankingRangeCommandValidator().Validate(new UpdateRankingRangeCommand(15, 35, "AQIDBAUGBwg="));

        result.IsValid.Should().BeTrue();
    }
}
