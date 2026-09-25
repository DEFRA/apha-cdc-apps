using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="SetProfileVersionPublicAccessCommand"/>.</summary>
public sealed class SetProfileVersionPublicAccessCommandValidator : AbstractValidator<SetProfileVersionPublicAccessCommand>
{
    /// <summary>Initialises a new instance of the <see cref="SetProfileVersionPublicAccessCommandValidator"/> class.</summary>
    public SetProfileVersionPublicAccessCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}
