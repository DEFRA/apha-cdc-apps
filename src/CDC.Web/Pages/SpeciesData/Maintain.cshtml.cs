using System.ComponentModel.DataAnnotations;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SpeciesData;

/// <summary>
/// Lets a species editor select a species or species group from the hierarchy and edit its
/// name and/or parent, recording a mandatory reason for change in the audit trail.
/// </summary>
/// <param name="speciesApiService">Typed client for the species endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class MaintainModel(ISpeciesApiService speciesApiService, ILogger<MaintainModel> logger)
    : BreadcrumbPageModelBase("Maintain species data")
{
    private const string SpeciesKey = "species";

    /// <summary>Gets the species hierarchy, built from every active species returned by the API.</summary>
    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    /// <summary>Gets a value indicating whether the species list failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the message to show the user when <see cref="HasError"/> is <see langword="true"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the message to show after a species has been selected but is otherwise invalid.</summary>
    public string? SelectionErrorMessage { get; private set; }

    /// <summary>Gets a confirmation message shown after a successful save.</summary>
    public string? SuccessMessage { get; private set; }

    /// <summary>Gets a value indicating whether the "Edit name/parent" section should be shown.</summary>
    public bool ShowEditPanel { get; private set; }

    /// <summary>Gets the species being edited, for the read-only "old name"/"old parent" display.</summary>
    public SpeciesDetailDto? SpeciesDetail { get; private set; }

    /// <summary>Gets the legal parent choices for the species being edited.</summary>
    public IReadOnlyList<SpeciesValidParentDto> ValidParents { get; private set; } = [];

    /// <summary>Gets a value indicating whether the audit trail table should be shown.</summary>
    public bool ShowAuditTrail { get; private set; }

    /// <summary>Gets every recorded species name/parent change, most recent first.</summary>
    public IReadOnlyList<SpeciesAuditTrailEntryDto> AuditTrail { get; private set; } = [];

    /// <summary>Gets the species selected in the tree, posted under the shared radio field name.</summary>
    [BindProperty(Name = SpeciesKey, SupportsGet = true)]
    public Guid? SelectedSpeciesId { get; set; }

    /// <summary>Gets or sets the "Edit name/parent" form fields.</summary>
    [BindProperty]
    public EditNameParentInput Input { get; set; } = new();

    /// <summary>Loads the species list and, if requested, re-selects a species on the tree.</summary>
    /// <param name="saved">Set by the redirect after a successful save, to show the confirmation banner.</param>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(bool saved, CancellationToken cancellationToken)
    {
        await LoadTreeAsync(cancellationToken);

        if (saved)
        {
            SuccessMessage = "The species name and parent were updated.";
        }
    }

    /// <summary>Loads and displays every recorded species name/parent change.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAuditTrailAsync(CancellationToken cancellationToken)
    {
        await LoadTreeAsync(cancellationToken);

        if (!HasError)
        {
            AuditTrail = await speciesApiService.GetSpeciesAuditTrailAsync(cancellationToken);
            ShowAuditTrail = true;
        }

        return Page();
    }

    /// <summary>Opens the "Edit name/parent" section for the species selected on the tree.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostEditNameParentAsync(CancellationToken cancellationToken)
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
