using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Shows the reference data audit trail, most recent change first, mirroring the legacy
/// spgReferenceTableAudit.
/// </summary>
/// <param name="referenceDataService">Reads the audit trail.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanManageReferenceData)]
public class ReferenceDataAuditModel(IReferenceDataApiService referenceDataService, ILogger<ReferenceDataAuditModel> logger)
    : BreadcrumbPageModelBase("Reference data audit trail")
{
    /// <summary>Gets or sets the table id to filter the trail by, if any.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid? TableId { get; set; }

    /// <summary>Gets the table the trail is filtered to, if one was supplied.</summary>
    public ReferenceTableSummary? Table { get; private set; }

    /// <summary>Gets every audit entry for the table, most recent first.</summary>
    public IReadOnlyList<ReferenceDataAuditEntry> Entries { get; private set; } = [];

    /// <summary>Gets the page of <see cref="Entries"/> to display.</summary>
    public IReadOnlyList<ReferenceDataAuditEntry> PagedEntries { get; private set; } = [];

    /// <summary>Gets or sets the page of entries shown, counting from 1.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the "Items per page" choice.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = ReferenceDataPaging.PageSizeOptions[0];

    /// <summary>Gets the number of pages of entries at the current page size.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets the message shown when the audit trail could not be loaded from CDC.Api.</summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>Loads the audit trail for the selected table.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (TableId is { } tableId)
            {
                Table = await referenceDataService.GetTableAsync(tableId, cancellationToken);
                TableId = Table?.Id;
            }

            // The legacy audit procedure reads one table at a time, so there is nothing to show without one.
            Entries = Table is null ? [] : await referenceDataService.GetAuditTrailAsync(Table.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.ReferenceDataApiFailed(exception);
            LoadErrorMessage = "We could not load the audit trail. Try again later.";
        }

        (PagedEntries, PageNumber, TotalPages) = ReferenceDataPaging.GetPage(Entries, PageNumber, PageSize);
    }

    /// <summary>Builds the URL of a page of entries, keeping the table and page size.</summary>
    public string? BuildPageUrl(int pageNumber) =>
        Url.Page("/CrossProfileAdmin/ReferenceDataAudit", new { tableId = TableId, PageNumber = pageNumber, PageSize });
}
