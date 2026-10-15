using FluentValidation;

namespace CDC.Api.Features.BespokeReportTemplates;

/// <summary>Validation rules shared by the commands that save a template.</summary>
internal static class BespokeReportTemplateRules
{
    /// <summary>The widest a title can be: <c>BespokeReportTemplate.Title</c> is <c>nvarchar(255)</c>.</summary>
    public const int MaxTitleLength = 255;

    /// <summary>The widest a ticked item can be: <c>BespokeReportTemplateCheckedItems.CheckedId</c> is <c>nvarchar(200)</c>.</summary>
    public const int MaxCheckedItemLength = 200;

    /// <summary>The most ticked items a template can hold: a profile node, its sections and a question and guidance node per question.</summary>
    public const int MaxCheckedItems = 1000;

    /// <summary>The legacy page only let letters, digits and spaces be typed into a title.</summary>
    public const string TitleCharacters = "^[A-Za-z0-9 ]+$";

    /// <summary>Requires a non-empty title of letters, digits and spaces that fits the column.</summary>
    /// <typeparam name="T">The validated type.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the title.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeATemplateTitle<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Template title is a required field.")
            .MaximumLength(MaxTitleLength).WithMessage($"Template title must be {MaxTitleLength} characters or fewer.")
            .Matches(TitleCharacters).WithMessage("Template title can only contain letters, numbers and spaces.");
}
