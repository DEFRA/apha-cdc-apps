using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Shows the reference data audit trail, most recent change first, mirroring the legacy
/// spgReferenceTableAudit.
/// </summary>
/// <param name="referenceDataService">Reads the audit trail.</param>
public class ReferenceDataAuditModel(IReferenceDataService referenceDataService)
    : BreadcrumbPageModelBase("Reference data audit trail")
{
    /// <summary>Gets or sets the table id to filter the trail by, if any.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid? TableId { get; set; }

    /// <summary>Gets the table the trail is filtered to, if one was supplied.</summary>
    public ReferenceTableSummary? Table { get; private set; }

    /// <summary>Gets the audit entries to display.</summary>
    public IReadOnlyList<ReferenceDataAuditEntry> Entries { get; private set; } = [];

    /// <summary>Loads the audit trail for the selected table, or for every table.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (TableId is { } tableId)
        {
            Table = await referenceDataService.GetTableAsync(tableId, cancellationToken);
            TableId = Table?.Id;
        }

        Entries = await referenceDataService.GetAuditTrailAsync(TableId, cancellationToken);
    }
}
