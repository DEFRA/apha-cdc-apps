using CDC.Api.Features.ProfileManagement.Dtos;
using FluentValidation;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Validates <see cref="UpdateProfileAttributesCommand"/>.</summary>
public sealed class UpdateProfileAttributesCommandValidator : AbstractValidator<UpdateProfileAttributesCommand>
{
    /// <summary>SQL Server <c>rowversion</c> values are always 8 bytes.</summary>
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="UpdateProfileAttributesCommandValidator"/> class.</summary>
    public UpdateProfileAttributesCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("A profile id is required.");

        RuleFor(command => command.LastUpdated)
            .NotNull().WithMessage("The row version read with the profile is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.");

        RuleForEach(command => command.AffectedSpeciesDeleteList)
            .SetValidator(new AffectedSpeciesDeleteDtoValidator());

        RuleForEach(command => command.AffectedSpeciesInsertList)
            .SetValidator(new AffectedSpeciesInsertDtoValidator());
    }
}

/// <summary>Validates an <see cref="AffectedSpeciesDeleteDto"/> supplied within a write command.</summary>
public sealed class AffectedSpeciesDeleteDtoValidator : AbstractValidator<AffectedSpeciesDeleteDto>
{
    /// <summary>Initialises a new instance of the <see cref="AffectedSpeciesDeleteDtoValidator"/> class.</summary>
    public AffectedSpeciesDeleteDtoValidator()
    {
        RuleFor(species => species.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required for each affected species.");

        RuleFor(species => species.SpeciesId)
            .NotEmpty().WithMessage("A species id is required for each affected species.");
    }
}
