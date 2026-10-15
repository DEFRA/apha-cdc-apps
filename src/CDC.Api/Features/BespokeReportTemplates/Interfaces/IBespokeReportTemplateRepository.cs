using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.BespokeReportTemplates.Interfaces;

/// <summary>
/// Data access for saved bespoke report templates: the <c>BespokeReportTemplate</c> and
/// <c>BespokeReportTemplateCheckedItems</c> tables the legacy NPoco data service read and wrote.
/// No stored procedures exist for them, so every call is a parameterised text command.
/// </summary>
public interface IBespokeReportTemplateRepository
{
    /// <summary>Reads every template's identifier and title, ordered by title.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The templates, without their ticked items.</returns>
    Task<IReadOnlyList<BespokeReportTemplate>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Reads one template with its ticked items.</summary>
    /// <param name="id">The template to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The template, or <see langword="null"/> when it does not exist.</returns>
    Task<BespokeReportTemplate?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds a template by its title, as the database's collation compares it.</summary>
    /// <param name="title">The title to look for.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The template without its ticked items, or <see langword="null"/> when none has the title.</returns>
    Task<BespokeReportTemplate?> GetByTitleAsync(string title, CancellationToken cancellationToken);

    /// <summary>Inserts a new template and its ticked items in one transaction.</summary>
    /// <param name="title">The template's title.</param>
    /// <param name="checkedItems">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The new template's identifier.</returns>
    Task<Guid> InsertAsync(string title, IReadOnlyList<string> checkedItems, CancellationToken cancellationToken);

    /// <summary>Replaces a template's ticked items in one transaction.</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="checkedItems">The ticked tree nodes.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>A task that completes when the items are saved.</returns>
    Task ReplaceCheckedItemsAsync(Guid id, IReadOnlyList<string> checkedItems, CancellationToken cancellationToken);

    /// <summary>Changes a template's title.</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the title is saved.</returns>
    Task UpdateTitleAsync(Guid id, string title, CancellationToken cancellationToken);

    /// <summary>Deletes a template and its ticked items in one transaction.</summary>
    /// <param name="id">The template to delete.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>A task that completes when the template is deleted.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
