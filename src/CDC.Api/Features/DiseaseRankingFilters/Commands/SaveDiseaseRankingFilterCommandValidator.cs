using FluentValidation;

namespace CDC.Api.Features.DiseaseRankingFilters.Commands;

/// <summary>Validates <see cref="SaveDiseaseRankingFilterCommand"/>.</summary>
public sealed class SaveDiseaseRankingFilterCommandValidator : AbstractValidator<SaveDiseaseRankingFilterCommand>
{
    /// <summary>Matches <c>DiseaseFilters.DiseaseName</c>'s <c>nvarchar(500)</c> column.</summary>
    private const int DiseaseNameMaxLength = 500;

    /// <summary>Matches <c>DiseaseFilters.Disease1</c>-<c>Disease4</c>'s <c>nvarchar(50)</c> columns.</summary>
    private const int DiseaseCriterionMaxLength = 50;

    private static readonly string[] ValidTransmissibleAgentTypes =
        ["bacteria", "virus", "fungus", "parasite", "other transmissible agent"];

    /// <summary>Initialises a new instance of the <see cref="SaveDiseaseRankingFilterCommandValidator"/> class.</summary>
    public SaveDiseaseRankingFilterCommandValidator()
    {
        RuleFor(command => command.Request.DiseaseName)
            .NotEmpty().WithMessage("Enter a report title.")
            .MaximumLength(DiseaseNameMaxLength).WithMessage($"The report title must be {DiseaseNameMaxLength} characters or fewer.");

        RuleFor(command => command.Request.Disease1)
            .NotEmpty().WithMessage("Select endemic diseases, exotic diseases or both.")
            .MaximumLength(DiseaseCriterionMaxLength).WithMessage("Select endemic diseases, exotic diseases or both.");

        RuleFor(command => command.Request.Disease2)
            .NotEmpty().WithMessage("Select zoonotic, non-zoonotic or both.")
            .MaximumLength(DiseaseCriterionMaxLength).WithMessage("Select zoonotic, non-zoonotic or both.");

        RuleFor(command => command.Request.Disease3)
            .NotEmpty().WithMessage("Select notifiable, non-notifiable or both.")
            .MaximumLength(DiseaseCriterionMaxLength).WithMessage("Select notifiable, non-notifiable or both.");

        RuleFor(command => command.Request.Disease4)
            .NotEmpty().WithMessage("Select infectious, non-infectious or both.")
            .MaximumLength(DiseaseCriterionMaxLength).WithMessage("Select infectious, non-infectious or both.");

        RuleForEach(command => command.Request.DiseaseTypes)
            .Must(type => ValidTransmissibleAgentTypes.Contains(type))
            .WithMessage("Transmissible agent type must be one of the recognised values.");

        RuleFor(command => command.Request.SpeciesIds)
            .NotEmpty().WithMessage("Select at least one species.");
    }
}
