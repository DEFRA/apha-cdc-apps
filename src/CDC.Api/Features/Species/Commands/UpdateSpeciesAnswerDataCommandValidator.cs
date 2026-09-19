using FluentValidation;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Validates <see cref="UpdateSpeciesAnswerDataCommand"/> before it reaches the database.
/// </summary>
public sealed class UpdateSpeciesAnswerDataCommandValidator : AbstractValidator<UpdateSpeciesAnswerDataCommand>
{
    /// <summary>SQL Server <c>rowversion</c> values are always 8 bytes.</summary>
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="UpdateSpeciesAnswerDataCommandValidator"/> class.</summary>
    public UpdateSpeciesAnswerDataCommandValidator()
    {
        RuleFor(command => command.SpeciesId)
            .NotEmpty().WithMessage("A species id is required.");

        RuleFor(command => command.LastUpdated)
            .NotNull().WithMessage("The row version read with the answer data is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.")
                .When(command => command.LastUpdated is not null);

        RuleFor(command => command.Changes)
            .NotNull().WithMessage("A changes collection is required.")
            .NotEmpty().WithMessage("At least one change is required.");

        RuleFor(command => command.Changes)
            .Must(changes => changes.Select(change => change.FieldId).Distinct().Count() == changes.Count)
            .WithMessage("Each field may only appear once in a change set.")
            .When(command => command.Changes is { Count: > 0 });

        RuleForEach(command => command.Changes).SetValidator(new SpeciesFieldValueChangeValidator());
    }
}

/// <summary>
/// Validates a single <see cref="SpeciesFieldValueChange"/>: the value supplied must match the
/// declared <see cref="SpeciesFieldValueKind"/>, because the stored procedure decides between
/// insert, update and delete on exactly those parameters.
/// </summary>
public sealed class SpeciesFieldValueChangeValidator : AbstractValidator<SpeciesFieldValueChange>
{
    /// <summary>Initialises a new instance of the <see cref="SpeciesFieldValueChangeValidator"/> class.</summary>
    public SpeciesFieldValueChangeValidator()
    {
        RuleFor(change => change.FieldId)
            .NotEmpty().WithMessage("A field id is required.");

        RuleFor(change => change.Kind)
            .IsInEnum().WithMessage("An unknown field value kind was supplied.");

        RuleFor(change => change.BooleanValue)
            .NotNull().WithMessage("A boolean value is required for a boolean field.")
            .When(change => change.Kind == SpeciesFieldValueKind.Boolean);

        RuleFor(change => change.ListValue)
            .NotNull().WithMessage("A list value is required for a list field.")
            .NotEqual(Guid.Empty).WithMessage("A list value must not be an empty GUID.")
            .When(change => change.Kind == SpeciesFieldValueKind.List);

        RuleFor(change => change.TextValue)
            .NotEmpty().WithMessage("A text value is required for a text field.")
            .When(change => change.Kind == SpeciesFieldValueKind.Text);

        RuleFor(change => change.MultiValues)
            .NotNull().WithMessage("A multi-value collection is required for a multi-value field.")
            .When(change => change.Kind == SpeciesFieldValueKind.MultiValue);

        RuleForEach(change => change.MultiValues)
            .NotEqual(Guid.Empty).WithMessage("A multi-value entry must not be an empty GUID.")
            .When(change => change.Kind == SpeciesFieldValueKind.MultiValue);

        RuleFor(change => change.MultiValues)
            .Must(values => values.Distinct().Count() == values.Count)
            .WithMessage("A multi-value field must not contain duplicate entries.")
            .When(change => change.Kind == SpeciesFieldValueKind.MultiValue && change.MultiValues is not null);

        // "None" clears the answer, so any supplied value would be silently discarded.
        RuleFor(change => change)
            .Must(change => change is
            {
                BooleanValue: null,
                ListValue: null,
                TextValue: null or "",
                MultiValues.Count: 0
            })
            .WithMessage("A change that clears a field must not supply a value.")
            .When(change => change.Kind == SpeciesFieldValueKind.None);
    }
}
