using FluentValidation;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Validates <see cref="ResolveInternalUserCommand"/>.</summary>
public sealed class ResolveInternalUserCommandValidator : AbstractValidator<ResolveInternalUserCommand>
{
    /// <summary>Initialises a new instance of the <see cref="ResolveInternalUserCommandValidator"/> class.</summary>
    public ResolveInternalUserCommandValidator()
    {
        RuleFor(command => command.SsoUserIdInt)
            .NotEmpty().WithMessage("An Entra ID object id is required.");

        RuleFor(command => command.UserName)
            .NotEmpty().WithMessage("A user name is required.")
            .MaximumLength(50).WithMessage("The user name must be 50 characters or fewer.");

        RuleFor(command => command.FullName)
            .MaximumLength(100).WithMessage("The full name must be 100 characters or fewer.");
    }
}
