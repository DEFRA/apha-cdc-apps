using FluentValidation;

namespace CDC.Api.Features.ProfileReports.Commands;

/// <summary>Validates <see cref="CreateProfileReportCommand"/>.</summary>
public sealed class CreateProfileReportCommandValidator : AbstractValidator<CreateProfileReportCommand>
{
    /// <summary>Initialises a new instance of the <see cref="CreateProfileReportCommandValidator"/> class.</summary>
    public CreateProfileReportCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.ProfileReportId)
            .NotEmpty().WithMessage("A profile report id is required.");

        RuleFor(command => command.ReportName)
            .NotEmpty().WithMessage("A report name is required.");

        RuleFor(command => command.ReportData)
            .NotNull().WithMessage("Report data is required.")
            .Must(data => data.Length > 0).WithMessage("Report data must not be empty.");
    }
}
