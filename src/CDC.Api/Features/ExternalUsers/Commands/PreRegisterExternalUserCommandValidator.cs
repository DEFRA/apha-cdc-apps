using FluentValidation;

namespace CDC.Api.Features.ExternalUsers.Commands;

/// <summary>Validates <see cref="PreRegisterExternalUserCommand"/>.</summary>
public sealed class PreRegisterExternalUserCommandValidator : AbstractValidator<PreRegisterExternalUserCommand>
{
    /// <summary>Initialises a new instance of the <see cref="PreRegisterExternalUserCommandValidator"/> class.</summary>
    public PreRegisterExternalUserCommandValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("Enter the full name.")
            .MaximumLength(100).WithMessage("The full name must be 100 characters or fewer.");

        RuleFor(command => command.EmailAddress)
            .NotEmpty().WithMessage("Enter the email address.")
            .EmailAddress().WithMessage("Enter an email address in the correct format e.g. name@example.com")
            .MaximumLength(50).WithMessage("The email address must be 50 characters or fewer.");

        RuleFor(command => command.Organisation)
            .NotEmpty().WithMessage("Enter the organisation.")
            .MaximumLength(100).WithMessage("The organisation must be 100 characters or fewer.");
    }
}
