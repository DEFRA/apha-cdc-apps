using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Lists the values held in a selected maintainable reference table, and is the entry point for
/// changing them and for viewing the audit trail.
/// </summary>
/// <param name="referenceDataService">Reads reference tables and their values.</param>
public class ReferenceDataModel(IReferenceDataService referenceDataService)
    : BreadcrumbPageModelBase("Maintain reference data")
{
    /// <summary>Gets or sets the id of the table chosen in the dropdown.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid? SelectedTableId { get; set; }

    /// <summary>Gets the maintainable reference tables offered in the dropdown.</summary>
    public IReadOnlyList<ReferenceTableSummary> Tables { get; private set; } = [];

    /// <summary>Gets the table matching <see cref="SelectedTableId"/>, if one was selected.</summary>
    public ReferenceTableSummary? SelectedTable { get; private set; }

    /// <summary>Gets the values belonging to <see cref="SelectedTable"/>.</summary>
    public IReadOnlyList<ReferenceDataValue> Values { get; private set; } = [];

    /// <summary>Gets or sets the success message shown after a change was saved on the edit page.</summary>
    [TempData]
    public string? ConfirmationMessage { get; set; }

    /// <summary>Loads the table list and, when a table is selected, its values.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Page renders static content only; no data to load.
    }
}
