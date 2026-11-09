using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>Shows the full detail of one recorded reference data change.</summary>
/// <param name="referenceDataService">Reads the audit trail.</param>
[Authorize(Policy = AuthorizationPolicies.CanManageReferenceData)]
public class ReferenceDataAuditDetailsModel(IReferenceDataApiService referenceDataService)
    : BreadcrumbPageModelBase("Reference data change details")
{
    /// <summary>Gets or sets the table the audit entry belongs to.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid TableId { get; set; }

    /// <summary>Gets or sets the identifier of the audit entry to display.</summary>
    [BindProperty(Name = "auditId", SupportsGet = true)]
    public Guid AuditId { get; set; }

    /// <summary>Gets the audit entry being displayed.</summary>
    public ReferenceDataAuditEntry? Entry { get; private set; }

    /// <summary>Loads the audit entry.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Entry = await referenceDataService.GetAuditEntryAsync(TableId, AuditId, cancellationToken);

        return Entry is null ? NotFound() : Page();
    }
}
