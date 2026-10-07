using FluentValidation;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Validates <see cref="AddSpeciesCommand"/> before it reaches the database. The user-facing
/// messages match the legacy "Add species data value" screen exactly.
/// </summary>
public sealed class AddSpeciesCommandValidator : AbstractValidator<AddSpeciesCommand>
{
    /// <summary>Matches the "Reason for change" input's limit and <c>@Reason varchar(255)</c>.</summary>
    private const int ReasonMaxLength = 255;

    /// <summary><c>spiSpecies</c> declares <c>@Name varchar(50)</c>, matching <c>Species.Name</c>.</summary>
    private const int NameMaxLength = 50;

    /// <summary>Initialises a new instance of the <see cref="AddSpeciesCommandValidator"/> class.</summary>
    public AddSpeciesCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("You need to provide a new name for this species")
            .MaximumLength(NameMaxLength)
                .WithMessage($"The new species name must be no longer than {NameMaxLength} characters");

        // Guid.Empty is a deliberate choice meaning "root species", so only an absent value fails.
        RuleFor(command => command.ParentId)
            .NotNull().WithMessage("You must select a new parent for the species");

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("You need to provide a reason for this change")
            .MaximumLength(ReasonMaxLength)
                .WithMessage($"The reason for change must be no longer than {ReasonMaxLength} characters");

        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("A user id is required to record the audit entry");
    }
}
