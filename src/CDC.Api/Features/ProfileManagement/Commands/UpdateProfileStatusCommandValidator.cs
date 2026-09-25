using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="UpdateProfileStatusCommand"/>.</summary>
public sealed class UpdateProfileStatusCommandValidator : AbstractValidator<UpdateProfileStatusCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UpdateProfileStatusCommandValidator"/> class.</summary>
    public UpdateProfileStatusCommandValidator()
    {
        RuleFor(command => command.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");

        RuleFor(command => command.ProfileStatusId)
            .NotEmpty().WithMessage("A profile status id is required.");
    }
}
