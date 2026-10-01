using FluentValidation;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>Validates <see cref="UploadStaticReportCommand"/>.</summary>
public sealed class UploadStaticReportCommandValidator : AbstractValidator<UploadStaticReportCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UploadStaticReportCommandValidator"/> class.</summary>
    public UploadStaticReportCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("A title is required.");

        RuleFor(command => command.PdfData)
            .NotEmpty().WithMessage("The file data is zero-length or the selected file is too large to upload.");

        RuleFor(command => command)
            .Must(command => !(command.IsPublic && command.IsUserManual))
            .WithMessage("User manuals cannot be made public.")
            .OverridePropertyName(nameof(UploadStaticReportCommand.IsPublic));
    }
}
