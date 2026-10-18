using CDC.Api.Domain.Common;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Interfaces;

/// <summary>
/// Application service for saved bespoke report templates. Replaces the legacy
/// <c>IBespokeReportTemplateService</c>: viewing needs a profile editor or policy profile user,
/// changing needs a profile editor, and the user-management system may do neither.
/// </summary>
public interface IBespokeReportTemplateService
{
    /// <summary>Lists the templates, ordered by title.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The templates; forbidden when the user may not view them.</returns>
    Task<Result<IReadOnlyList<BespokeReportTemplateSummaryDto>>> GetTemplatesAsync(CancellationToken cancellationToken);

    /// <summary>Gets one template with its ticked items.</summary>
    /// <param name="id">The template to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The template; forbidden when the user may not view it, not found when it does not exist.</returns>
    Task<Result<BespokeReportTemplateDto>> GetTemplateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Saves the ticked items as a new template ("Save template as").</summary>
    /// <param name="title">The new template's title.</param>
    /// <param name="checkedList">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template; forbidden when the user may not edit templates, a conflict when the title is already in use.</returns>
    Task<Result<BespokeReportTemplateDto>> SaveAsNewAsync(string title, IReadOnlyList<string> checkedList, CancellationToken cancellationToken);

    /// <summary>Replaces an existing template's ticked items ("Update template").</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="checkedList">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template; forbidden when the user may not edit templates, not found when it does not exist.</returns>
    Task<Result<BespokeReportTemplateDto>> UpdateCheckedListAsync(Guid id, IReadOnlyList<string> checkedList, CancellationToken cancellationToken);

    /// <summary>Changes a template's title ("Edit title").</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved template; forbidden when the user may not edit templates, not found when it does not exist, a conflict when another template has the title.</returns>
    Task<Result<BespokeReportTemplateDto>> RenameAsync(Guid id, string title, CancellationToken cancellationToken);

    /// <summary>Deletes a template.</summary>
    /// <param name="id">The template to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success; forbidden when the user may not edit templates, not found when it does not exist.</returns>
    Task<Result<Unit>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
