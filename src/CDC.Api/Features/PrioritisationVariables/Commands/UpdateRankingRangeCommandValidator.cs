using FluentValidation;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>Validates <see cref="UpdateRankingRangeCommand"/>.</summary>
public sealed class UpdateRankingRangeCommandValidator : AbstractValidator<UpdateRankingRangeCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UpdateRankingRangeCommandValidator"/> class.</summary>
    public UpdateRankingRangeCommandValidator()
    {
        RuleFor(command => command.LowerBound)
            .InclusiveBetween(0, 998).WithMessage("Lower bound must be a positive integer");

        RuleFor(command => command.UpperBound)
            .InclusiveBetween(1, 999).WithMessage("Upper bound must be a positive integer");

        RuleFor(command => command)
            .Must(command => command.LowerBound < command.UpperBound)
            .WithMessage("Lower bound must be a positive integer that is lower than upper bound")
            .OverridePropertyName(nameof(UpdateRankingRangeCommand.LowerBound));

        RuleFor(command => command.RowVersion)
            .NotEmpty().WithMessage("A row version is required.");
    }
}
