using FluentValidation;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Validates <see cref="LookUpUserByEmailCommand"/>.</summary>
public sealed class LookUpUserByEmailCommandValidator : AbstractValidator<LookUpUserByEmailCommand>
{
    /// <summary>Initialises a new instance of the <see cref="LookUpUserByEmailCommandValidator"/> class.</summary>
    public LookUpUserByEmailCommandValidator()
    {
        RuleFor(command => command.EmailAddress)
            .NotEmpty().WithMessage("An email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(50).WithMessage("The email address must be 50 characters or fewer.");
    }
}
