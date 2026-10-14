using FluentValidation;

namespace CDC.Api.Features.Species.Commands;

/// <summary>Validates <see cref="ChangeSpeciesPositionCommand"/>.</summary>
public sealed class ChangeSpeciesPositionCommandValidator : AbstractValidator<ChangeSpeciesPositionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="ChangeSpeciesPositionCommandValidator"/> class.</summary>
    public ChangeSpeciesPositionCommandValidator()
    {
        RuleFor(command => command.SpeciesId)
            .NotEmpty().WithMessage("A species id is required.");
    }
}
