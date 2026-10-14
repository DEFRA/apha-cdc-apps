using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Commands;

/// <summary>Saves the ticked tree nodes as a new bespoke report template ("Save template as").</summary>
/// <param name="Title">The new template's title.</param>
/// <param name="CheckedList">The ticked tree nodes.</param>
public sealed record SaveBespokeReportTemplateCommand(string Title, IReadOnlyList<string> CheckedList) : IRequest<Result<BespokeReportTemplateDto>>;

/// <summary>Validates <see cref="SaveBespokeReportTemplateCommand"/>.</summary>
public sealed class SaveBespokeReportTemplateCommandValidator : AbstractValidator<SaveBespokeReportTemplateCommand>
{
    /// <summary>Initialises a new instance of the <see cref="SaveBespokeReportTemplateCommandValidator"/> class.</summary>
    public SaveBespokeReportTemplateCommandValidator()
    {
        RuleFor(command => command.Title).MustBeATemplateTitle();

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

/// <summary>Handles <see cref="SaveBespokeReportTemplateCommand"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class SaveBespokeReportTemplateCommandHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<SaveBespokeReportTemplateCommand, Result<BespokeReportTemplateDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template.</returns>
    public Task<Result<BespokeReportTemplateDto>> Handle(SaveBespokeReportTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.SaveAsNewAsync(request.Title, request.CheckedList, cancellationToken);
    }
}
