using FluentValidation;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Validates <see cref="InactivateSpeciesCommand"/> before it reaches the database.
/// </summary>
public sealed class InactivateSpeciesCommandValidator : AbstractValidator<InactivateSpeciesCommand>
{
    /// <summary>SQL Server <c>rowversion</c> values are always 8 bytes.</summary>
    private const int RowVersionLength = 8;

    /// <summary>Matches the <c>txtReasonForChange</c> input's limit.</summary>
    private const int ReasonMaxLength = 255;

    /// <summary>Initialises a new instance of the <see cref="InactivateSpeciesCommandValidator"/> class.</summary>
    public InactivateSpeciesCommandValidator()
    {
        RuleFor(command => command.SpeciesId)
            .NotEmpty().WithMessage("A species id is required.");

        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("A user id is required to record the audit entry.");

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("You need to provide a reason for this change.")
            .MaximumLength(ReasonMaxLength).WithMessage($"The reason for change must be no longer than {ReasonMaxLength} characters.");

        RuleFor(command => command.LastUpdated)
            .NotNull().WithMessage("The row version read with the species detail is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.")
                .When(command => command.LastUpdated is not null);
    }
}
