using FluentValidation;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>Validates <see cref="DeleteStaticReportVersionCommand"/>.</summary>
public sealed class DeleteStaticReportVersionCommandValidator : AbstractValidator<DeleteStaticReportVersionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="DeleteStaticReportVersionCommandValidator"/> class.</summary>
    public DeleteStaticReportVersionCommandValidator()
    {
        RuleFor(command => command.StaticReportVersionId)
            .NotEmpty().WithMessage("A static report version id is required.");
    }
}
