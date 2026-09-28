using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="DeleteProfileVersionCommand"/>.</summary>
public sealed class DeleteProfileVersionCommandValidator : AbstractValidator<DeleteProfileVersionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="DeleteProfileVersionCommandValidator"/> class.</summary>
    public DeleteProfileVersionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}
