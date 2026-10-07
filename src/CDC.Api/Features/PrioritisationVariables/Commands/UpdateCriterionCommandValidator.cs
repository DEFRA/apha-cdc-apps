using FluentValidation;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>Validates <see cref="UpdateCriterionCommand"/>.</summary>
public sealed class UpdateCriterionCommandValidator : AbstractValidator<UpdateCriterionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UpdateCriterionCommandValidator"/> class.</summary>
    public UpdateCriterionCommandValidator()
    {
        RuleFor(command => command.CriterionId)
            .NotEmpty().WithMessage("A criterion id is required.");

        RuleFor(command => command.Weight)
            .InclusiveBetween(1, 999).WithMessage("Criterion weight must be a positive integer.");

        RuleForEach(command => command.ValueScores).ChildRules(valueScore =>
        {
            valueScore.RuleFor(v => v.ValueId)
                .NotEmpty().WithMessage("A criterion value id is required.");

            valueScore.RuleFor(v => v.Score)
                .InclusiveBetween(0, 999).WithMessage("Criterion value score must be a positive integer.");
        });
    }
}
