using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>Validates <see cref="UpdateCriterionCommand"/>.</summary>
public sealed partial class UpdateCriterionCommandValidator : AbstractValidator<UpdateCriterionCommand>
{
    /// <summary>The legacy page's limit on the criterion text once its formatting is removed.</summary>
    private const int MaxNameLength = 255;

    /// <summary>Initialises a new instance of the <see cref="UpdateCriterionCommandValidator"/> class.</summary>
    public UpdateCriterionCommandValidator()
    {
        RuleFor(command => command.CriterionId)
            .NotEmpty().WithMessage("A criterion id is required.");

        RuleFor(command => command.Weight)
            .InclusiveBetween(1, 999).WithMessage("Criterion weight must be a positive integer.");

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(PlainText(name))).WithMessage("You must enter criterion text.")
            .Must(name => PlainText(name).Length <= MaxNameLength).WithMessage($"The criterion text must be no longer than {MaxNameLength} characters.")
            .When(command => command.Name is not null);

        RuleForEach(command => command.ValueScores).ChildRules(valueScore =>
        {
            valueScore.RuleFor(v => v.ValueId)
                .NotEmpty().WithMessage("A criterion value id is required.");

            valueScore.RuleFor(v => v.Score)
                .InclusiveBetween(0, 999).WithMessage("Criterion value score must be a positive integer.");
        });
    }

    private static string PlainText(string? name) =>
        WebUtility.HtmlDecode(MarkupRegex().Replace(name ?? string.Empty, string.Empty)).Trim();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex MarkupRegex();
}
