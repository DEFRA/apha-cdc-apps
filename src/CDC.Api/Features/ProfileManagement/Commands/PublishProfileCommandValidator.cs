using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="PublishProfileCommand"/>.</summary>
public sealed class PublishProfileCommandValidator : AbstractValidator<PublishProfileCommand>
{
    /// <summary>Initialises a new instance of the <see cref="PublishProfileCommandValidator"/> class.</summary>
    public PublishProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");
    }
}
