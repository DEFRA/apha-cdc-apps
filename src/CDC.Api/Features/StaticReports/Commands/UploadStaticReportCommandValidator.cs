using FluentValidation;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>Validates <see cref="UploadStaticReportCommand"/>.</summary>
public sealed class UploadStaticReportCommandValidator : AbstractValidator<UploadStaticReportCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UploadStaticReportCommandValidator"/> class.</summary>
    public UploadStaticReportCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("A report title is required.")
            .MaximumLength(255).WithMessage("The report title cannot exceed 255 characters.");

        RuleFor(command => command.PdfData)
            .NotNull().WithMessage("Pdf data is required.")
            .Must(data => data.Length > 0).WithMessage("Pdf data must not be empty.");
    }
}
