using FluentValidation;

namespace CDC.Api.Features.ProfileQuestions.Commands;

/// <summary>Validates <see cref="UpdateProfileQuestionCommand"/>.</summary>
public sealed class UpdateProfileQuestionCommandValidator : AbstractValidator<UpdateProfileQuestionCommand>
{
    /// <summary>SQL Server <c>rowversion</c> values are always 8 bytes.</summary>
    private const int RowVersionLength = 8;

    /// <summary><c>Name</c>/<c>NonTechnicalName</c> are <c>nvarchar(200)</c> in the legacy schema.</summary>
    private const int NameMaxLength = 200;

    /// <summary>Initialises a new instance of the <see cref="UpdateProfileQuestionCommandValidator"/> class.</summary>
    public UpdateProfileQuestionCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("A question id is required.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("A question name is required.")
            .MaximumLength(NameMaxLength).WithMessage($"The name must be {NameMaxLength} characters or fewer.");

        RuleFor(command => command.NonTechnicalName)
            .NotEmpty().WithMessage("A non-technical name is required.")
            .MaximumLength(NameMaxLength).WithMessage($"The non-technical name must be {NameMaxLength} characters or fewer.");

        RuleFor(command => command.UserGuidance)
            .NotEmpty().WithMessage("Guidance text is required.");

        RuleFor(command => command.LastUpdated)
            .NotNull().WithMessage("The row version read with the question is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.");
    }
}
