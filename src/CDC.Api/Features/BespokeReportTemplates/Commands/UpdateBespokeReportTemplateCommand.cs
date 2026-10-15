using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Commands;

/// <summary>Replaces a bespoke report template's ticked tree nodes ("Update template").</summary>
/// <param name="Id">The template to change.</param>
/// <param name="CheckedList">The ticked tree nodes.</param>
public sealed record UpdateBespokeReportTemplateCommand(Guid Id, IReadOnlyList<string> CheckedList) : IRequest<Result<BespokeReportTemplateDto>>;

/// <summary>Validates <see cref="UpdateBespokeReportTemplateCommand"/>.</summary>
public sealed class UpdateBespokeReportTemplateCommandValidator : AbstractValidator<UpdateBespokeReportTemplateCommand>
{
    /// <summary>Initialises a new instance of the <see cref="UpdateBespokeReportTemplateCommandValidator"/> class.</summary>
    public UpdateBespokeReportTemplateCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("A template id is required.");

        RuleFor(command => command.CheckedList)
            .NotNull().WithMessage("The ticked items are required.")
            .Must(items => items is null || items.Count <= BespokeReportTemplateRules.MaxCheckedItems)
            .WithMessage($"A template can hold at most {BespokeReportTemplateRules.MaxCheckedItems} ticked items.");

        RuleForEach(command => command.CheckedList)
            .NotEmpty().WithMessage("A ticked item cannot be empty.")
            .MaximumLength(BespokeReportTemplateRules.MaxCheckedItemLength)
            .WithMessage($"A ticked item must be {BespokeReportTemplateRules.MaxCheckedItemLength} characters or fewer.");
    }
}

/// <summary>Handles <see cref="UpdateBespokeReportTemplateCommand"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class UpdateBespokeReportTemplateCommandHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<UpdateBespokeReportTemplateCommand, Result<BespokeReportTemplateDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template.</returns>
    public Task<Result<BespokeReportTemplateDto>> Handle(UpdateBespokeReportTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.UpdateCheckedListAsync(request.Id, request.CheckedList, cancellationToken);
    }
}
