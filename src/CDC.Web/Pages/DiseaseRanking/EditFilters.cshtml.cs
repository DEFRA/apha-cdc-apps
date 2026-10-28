using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.DiseaseRanking;

/// <summary>
/// Lets a profile editor create, update and delete disease ranking filters: a species selection
/// plus a set of disease criteria, used to produce disease ranking reports. Replaces the legacy
/// <c>EditDiseaseRankingFilters.aspx</c> page.
/// </summary>
/// <param name="diseaseRankingFiltersApiService">Typed client for the disease ranking filter endpoints on CDC.Api.</param>
/// <param name="speciesApiService">Typed client for the species endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanManageDiseaseRankingFilters)]
public class EditFiltersModel(
    IDiseaseRankingFiltersApiService diseaseRankingFiltersApiService,
    ISpeciesApiService speciesApiService,
    ILogger<EditFiltersModel> logger)
    : BreadcrumbPageModelBase("Edit disease ranking filters")
{
    private const string SpeciesFieldName = "species";

    private static readonly IReadOnlyList<string> TransmissibleAgentTypes =
        ["bacteria", "virus", "fungus", "parasite", "other transmissible agent"];

    /// <summary>Gets every active filter, for the "Select Filter" dropdown.</summary>
    public IReadOnlyList<DiseaseRankingFilterSummaryDto> Filters { get; private set; } = [];

    /// <summary>Gets the species hierarchy, built from every active species returned by the API.</summary>
    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    /// <summary>Gets every transmissible agent type offered by the checkbox list.</summary>
    public static IReadOnlyList<string> AgentTypeChoices => TransmissibleAgentTypes;

    /// <summary>Gets a value indicating whether the filters or species list failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the message to show the user when <see cref="HasError"/> is <see langword="true"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the message to show after a filter has been selected but is otherwise invalid.</summary>
    public string? SelectionErrorMessage { get; private set; }

    /// <summary>Gets a confirmation message shown after a successful save or delete.</summary>
    public string? SuccessMessage { get; private set; }

    /// <summary>Gets a value indicating whether the species/criteria panel should be shown.</summary>
    public bool ShowFilterPanel { get; private set; }

    /// <summary>Gets a value indicating whether the panel is creating a new filter rather than editing an existing one.</summary>
    public bool IsNewFilter { get; private set; } = true;

    /// <summary>Gets or sets the filter selected in the dropdown.</summary>
    [BindProperty(Name = "SelectedFilterNumber", SupportsGet = true)]
    public long? SelectedFilterNumber { get; set; }

    /// <summary>Gets or sets the species selected on the tree, posted under the shared checkbox field name.</summary>
    [BindProperty(Name = "SpeciesIds")]
    public IReadOnlyList<Guid> SelectedSpeciesIds { get; set; } = [];

    /// <summary>Gets or sets the transmissible agent types checked on the form.</summary>
    /// <remarks>
    /// Bound at the top level, not as <c>Input.DiseaseTypes</c>: ASP.NET Core doesn't reliably bind a
    /// non-indexed repeated-key checkbox group when the target property is nested inside another
    /// bound complex object (<see cref="Input"/>) - it always binds to an empty list. Posting under
    /// the same <c>Input.DiseaseTypes</c> form field name but binding it here, at the root, works.
    /// </remarks>
    [BindProperty(Name = "Input.DiseaseTypes")]
    public IReadOnlyList<string> SelectedDiseaseTypes { get; set; } = [];

    /// <summary>Gets or sets the filter name and criteria form fields.</summary>
    [BindProperty]
    public SaveDiseaseRankingFilterInput Input { get; set; } = new();

    /// <summary>Loads the filter dropdown and, if requested, re-selects and displays a filter.</summary>
    /// <param name="created">Set by the redirect after a filter is created, to show the confirmation banner.</param>
    /// <param name="saved">Set by the redirect after a filter is updated, to show the confirmation banner.</param>
    /// <param name="deleted">Set by the redirect after a filter is deleted, to show the confirmation banner.</param>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(bool created, bool saved, bool deleted, CancellationToken cancellationToken)
    {
        await LoadFiltersAsync(cancellationToken);

        if (!HasError && SelectedFilterNumber is { } filterNumber)
        {
            await DisplayFilterAsync(filterNumber, cancellationToken);
        }

        if (created)
        {
            SuccessMessage = "Filter value successfully saved";
        }
        else if (saved)
        {
            SuccessMessage = "Filter value successfully updated";
        }
        else if (deleted)
        {
            SuccessMessage = "Filter value successfully deleted";
        }
    }

    /// <summary>Opens a blank species/criteria panel to create a new filter.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostCreateNewAsync(CancellationToken cancellationToken)
    {
        // This handler doesn't post the criteria form, so drop the [Required] errors that
        // model binding still raised against the (unposted, empty) Input fields.
        ModelState.Clear();

        await LoadFiltersAsync(cancellationToken);

        if (HasError)
        {
            return Page();
        }

        SelectedFilterNumber = null;
        Input = new SaveDiseaseRankingFilterInput();
        IsNewFilter = true;
        ShowFilterPanel = true;

        await LoadSpeciesTreeAsync([], cancellationToken);

        return Page();
    }

    /// <summary>Opens the species/criteria panel for the filter chosen in the dropdown.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostSelectFilterAsync(CancellationToken cancellationToken)
    {
        // As above: this handler only posts the dropdown, not the criteria form.
        ModelState.Clear();

        await LoadFiltersAsync(cancellationToken);

        if (HasError)
        {
            return Page();
        }

        if (SelectedFilterNumber is null)
        {
            SelectionErrorMessage = "Select a filter.";
            return Page();
        }

        await DisplayFilterAsync(SelectedFilterNumber.Value, cancellationToken);

        return Page();
    }

    /// <summary>Validates and saves the filter: creates a new filter, or updates an existing one.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        await LoadFiltersAsync(cancellationToken);

        if (HasError)
        {
            return Page();
        }

        IsNewFilter = Input.FilterNumber is null;
        ShowFilterPanel = true;

        if (SelectedSpeciesIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select the species from the list");
        }

        if (!ModelState.IsValid)
        {
            await LoadSpeciesTreeAsync(SelectedSpeciesIds, cancellationToken);
            return Page();
        }

        var request = new SaveDiseaseRankingFilterRequestDto
        {
            DiseaseName = Input.DiseaseName!.Trim(),
            Disease1 = Input.Disease1!,
            Disease2 = Input.Disease2!,
            Disease3 = Input.Disease3!,
            Disease4 = Input.Disease4!,
            DiseaseTypes = SelectedDiseaseTypes,
            SpeciesIds = SelectedSpeciesIds
        };

        var result = Input.FilterNumber is { } filterNumber
            ? await diseaseRankingFiltersApiService.UpdateFilterAsync(filterNumber, request, cancellationToken)
            : await diseaseRankingFiltersApiService.CreateFilterAsync(request, cancellationToken);

        if (result.Outcome != DiseaseRankingFilterSaveOutcome.Success)
        {
            logger.SaveFailed(Input.FilterNumber, result.Outcome);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not save this filter. Try again later.");
            await LoadSpeciesTreeAsync(SelectedSpeciesIds, cancellationToken);
            return Page();
        }

        logger.Saved(result.FilterNumber, IsNewFilter);

        // Post-redirect-get: shows the confirmation banner without resubmitting the form on refresh.
        return IsNewFilter
            ? RedirectToPage(new { created = true })
            : RedirectToPage(new { filter = result.FilterNumber, saved = true });
    }

    /// <summary>Deletes a filter.</summary>
    /// <param name="filterNumber">The filter to delete.</param>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostDeleteAsync(long filterNumber, CancellationToken cancellationToken)
    {
        // This handler only posts the hidden filter number, not the criteria form.
        ModelState.Clear();

        await diseaseRankingFiltersApiService.DeleteFilterAsync(filterNumber, cancellationToken);

        logger.Deleted(filterNumber);

        return RedirectToPage(new { deleted = true });
    }

    private async Task DisplayFilterAsync(long filterNumber, CancellationToken cancellationToken)
    {
        var detail = await diseaseRankingFiltersApiService.GetFilterDetailAsync(filterNumber, cancellationToken);

        if (detail is null)
        {
            SelectionErrorMessage = "The selected filter could not be found. It may have been deleted.";
            return;
        }

        SelectedFilterNumber = filterNumber;
        IsNewFilter = false;
        ShowFilterPanel = true;

        Input = new SaveDiseaseRankingFilterInput
        {
            FilterNumber = detail.FilterNumber,
            DiseaseName = detail.DiseaseName,
            Disease1 = detail.Disease1,
            Disease2 = detail.Disease2,
            Disease3 = detail.Disease3,
            Disease4 = detail.Disease4,
            DiseaseTypes = detail.DiseaseTypes
        };
        SelectedSpeciesIds = detail.SpeciesIds;

        await LoadSpeciesTreeAsync(detail.SpeciesIds, cancellationToken);
    }

    private async Task LoadFiltersAsync(CancellationToken cancellationToken)
    {
        try
        {
            Filters = await diseaseRankingFiltersApiService.GetFiltersAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LoadFailed(exception);
            HasError = true;
            ErrorMessage = "We could not load the disease ranking filters. Try again later.";
        }
    }

    private async Task LoadSpeciesTreeAsync(IReadOnlyList<Guid> selectedSpeciesIds, CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = SpeciesFieldName,
                FieldName = "SpeciesIds",
                Nodes = SpeciesTreeBuilder.Build(species),
                ItemNameSingular = SpeciesFieldName,
                ItemNamePlural = SpeciesFieldName,
                AllowMultipleSelection = true,
                EnforceHierarchyExclusivity = false,
                SelectedValues = selectedSpeciesIds.Select(id => id.ToString()).ToHashSet(),
                EmptySelectionLabel = "Any species"
            };
        }
        catch (HttpRequestException exception)
        {
            logger.LoadFailed(exception);
            HasError = true;
            ErrorMessage = "We could not load the species list. Try again later.";
        }
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = SpeciesFieldName,
        FieldName = "SpeciesIds",
        Nodes = [],
        AllowMultipleSelection = true,
        EnforceHierarchyExclusivity = false
    };
}
