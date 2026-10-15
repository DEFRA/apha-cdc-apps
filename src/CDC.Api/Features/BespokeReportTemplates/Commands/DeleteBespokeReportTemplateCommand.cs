using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Commands;

/// <summary>Deletes a bespoke report template.</summary>
/// <param name="Id">The template to delete.</param>
public sealed record DeleteBespokeReportTemplateCommand(Guid Id) : IRequest<Result<Unit>>;

/// <summary>Validates <see cref="DeleteBespokeReportTemplateCommand"/>.</summary>
public sealed class DeleteBespokeReportTemplateCommandValidator : AbstractValidator<DeleteBespokeReportTemplateCommand>
{
    /// <summary>Initialises a new instance of the <see cref="DeleteBespokeReportTemplateCommandValidator"/> class.</summary>
    public DeleteBespokeReportTemplateCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("A template id is required.");
    }
}

/// <summary>Handles <see cref="DeleteBespokeReportTemplateCommand"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class DeleteBespokeReportTemplateCommandHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<DeleteBespokeReportTemplateCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the template is deleted.</returns>
    public Task<Result<Unit>> Handle(DeleteBespokeReportTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.DeleteAsync(request.Id, cancellationToken);
    }
}
