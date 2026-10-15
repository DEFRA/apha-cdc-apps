using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Commands;

/// <summary>Changes a bespoke report template's title ("Edit title").</summary>
/// <param name="Id">The template to change.</param>
/// <param name="Title">The new title.</param>
public sealed record RenameBespokeReportTemplateCommand(Guid Id, string Title) : IRequest<Result<BespokeReportTemplateDto>>;

/// <summary>Validates <see cref="RenameBespokeReportTemplateCommand"/>.</summary>
public sealed class RenameBespokeReportTemplateCommandValidator : AbstractValidator<RenameBespokeReportTemplateCommand>
{
    /// <summary>Initialises a new instance of the <see cref="RenameBespokeReportTemplateCommandValidator"/> class.</summary>
    public RenameBespokeReportTemplateCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("A template id is required.");
        RuleFor(command => command.Title).MustBeATemplateTitle();
    }
}

/// <summary>Handles <see cref="RenameBespokeReportTemplateCommand"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class RenameBespokeReportTemplateCommandHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<RenameBespokeReportTemplateCommand, Result<BespokeReportTemplateDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template.</returns>
    public Task<Result<BespokeReportTemplateDto>> Handle(RenameBespokeReportTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.RenameAsync(request.Id, request.Title, cancellationToken);
    }
}
