using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the bespoke report template endpoints on CDC.Api.</summary>
public interface IBespokeReportTemplatesApiService
{
    /// <summary>Calls <c>GET /api/bespoke-report-templates</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The templates, ordered by title, or why they are not available.</returns>
    Task<BespokeTemplateListResult> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/bespoke-report-templates/{id}</c>.</summary>
    /// <param name="id">The template to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The template with its ticked items, or why it is not available.</returns>
    Task<BespokeTemplateResult> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/bespoke-report-templates</c> to save the ticked items as a new template.</summary>
    /// <param name="title">The new template's title.</param>
    /// <param name="checkedList">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template, or why it was not saved.</returns>
    Task<BespokeTemplateResult> SaveAsAsync(string title, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/bespoke-report-templates/{id}</c> to replace a template's ticked items.</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="checkedList">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template, or why it was not saved.</returns>
    Task<BespokeTemplateResult> UpdateAsync(Guid id, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/bespoke-report-templates/{id}/title</c> to change a template's title.</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template, or why it was not saved.</returns>
    Task<BespokeTemplateResult> RenameAsync(Guid id, string title, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/bespoke-report-templates/{id}</c>.</summary>
    /// <param name="id">The template to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the template was deleted.</returns>
    Task<BespokeTemplatesOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
