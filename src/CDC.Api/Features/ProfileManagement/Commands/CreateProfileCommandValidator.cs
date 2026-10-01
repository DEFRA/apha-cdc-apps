using CDC.Api.Features.ProfileManagement.Dtos;
using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="CreateProfileCommand"/>.</summary>
public sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    /// <summary>Initialises a new instance of the <see cref="CreateProfileCommandValidator"/> class.</summary>
    public CreateProfileCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("A profile id is required.");

        RuleFor(command => command.CurrentDraftProfileVersionId)
            .NotEmpty().WithMessage("An initial profile version id is required.");

        // A scenario's initial version title comes from ParentTitle; a current-situation
        // profile's comes from Title. Exactly one of the two is required, matching the legacy
        // CreateProfile behaviour.
        RuleFor(command => command.ParentTitle)
            .NotEmpty().WithMessage("A parent title is required when creating a scenario.")
            .When(command => command.ParentId != Guid.Empty);

        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("A title is required when creating a current-situation profile.")
            .When(command => command.ParentId == Guid.Empty);

        RuleForEach(command => command.AffectedSpeciesInsertList)
            .SetValidator(new AffectedSpeciesInsertDtoValidator());
    }
}

/// <summary>Validates an <see cref="AffectedSpeciesInsertDto"/> supplied within a write command.</summary>
public sealed class AffectedSpeciesInsertDtoValidator : AbstractValidator<AffectedSpeciesInsertDto>
{
    /// <summary>Initialises a new instance of the <see cref="AffectedSpeciesInsertDtoValidator"/> class.</summary>
    public AffectedSpeciesInsertDtoValidator()
    {
        RuleFor(species => species.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required for each affected species.");

        RuleFor(species => species.SpeciesId)
            .NotEmpty().WithMessage("A species id is required for each affected species.");

        RuleFor(species => species.Type)
            .NotEmpty().WithMessage("An affected species type is required.")
            .Must(type => type is "Profiled" or "Other")
                .WithMessage("The affected species type must be 'Profiled' or 'Other'.");
    }
}
