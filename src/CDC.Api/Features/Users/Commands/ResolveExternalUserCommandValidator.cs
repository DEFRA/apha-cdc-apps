using FluentValidation;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Validates <see cref="ResolveExternalUserCommand"/>.</summary>
public sealed class ResolveExternalUserCommandValidator : AbstractValidator<ResolveExternalUserCommand>
{
    /// <summary>Initialises a new instance of the <see cref="ResolveExternalUserCommandValidator"/> class.</summary>
    public ResolveExternalUserCommandValidator()
    {
        RuleFor(command => command.SsoUserIdExt)
            .NotEmpty().WithMessage("A CIDM subject id is required.");

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("An email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(50).WithMessage("The email address must be 50 characters or fewer.");

        RuleFor(command => command.FirstName)
            .MaximumLength(100).WithMessage("The first name must be 100 characters or fewer.");

        RuleFor(command => command.LastName)
            .MaximumLength(100).WithMessage("The last name must be 100 characters or fewer.");

        RuleFor(command => command.Organisation)
            .MaximumLength(100).WithMessage("The organisation must be 100 characters or fewer.");
    }
}
