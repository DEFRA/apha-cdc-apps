using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="CreateNewProfileVersionCommand"/>.</summary>
public sealed class CreateNewProfileVersionCommandValidator : AbstractValidator<CreateNewProfileVersionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="CreateNewProfileVersionCommandValidator"/> class.</summary>
    public CreateNewProfileVersionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        // Mirrors the legacy rule "you cannot make a draft profile public": IsPublic only
        // makes sense alongside IsPublished. Checked here as a pure input-shape rule; the
        // remaining business rules require reading the current version and are enforced by
        // the repository within the same transaction.
        RuleFor(command => command.IsPublic)
            .Equal(false).WithMessage("A draft profile version cannot be made public.")
            .When(command => !command.IsPublished);
    }
}
