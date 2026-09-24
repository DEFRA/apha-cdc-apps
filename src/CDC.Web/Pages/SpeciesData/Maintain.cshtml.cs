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
        await LoadTreeAsync(cancellationToken);

        if (HasError)
        {
            return Page();
        }

        if (SelectedSpeciesId is null)
        {
            SelectionErrorMessage = "Select a species or species group to edit.";
            return Page();
        }

        var detail = await speciesApiService.GetSpeciesDetailAsync(SelectedSpeciesId.Value, cancellationToken);

        if (detail is null)
        {
            SelectionErrorMessage = "The selected species could not be found. It may have been removed.";
            return Page();
        }

        await OpenEditPanelAsync(detail, cancellationToken);

        return Page();
    }

    /// <summary>Validates and applies a name/parent change.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        await LoadTreeAsync(cancellationToken);

        if (HasError)
        {
            return Page();
        }

        ValidateInput();

        if (!ModelState.IsValid)
        {
            await RedisplayEditPanelAsync(cancellationToken);
            return Page();
        }

        byte[] lastUpdated;
        try
        {
            lastUpdated = Convert.FromBase64String(Input.LastUpdatedBase64);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The species could not be saved. Reload and try again.");
            await RedisplayEditPanelAsync(cancellationToken);
            return Page();
        }

        var result = await speciesApiService.UpdateSpeciesNameParentAsync(
            new UpdateSpeciesNameParentRequestDto
            {
                SpeciesId = Input.SpeciesId,
                Name = Input.Name!.Trim(),
                ParentId = Input.ParentId ?? Guid.Empty,
                Reason = Input.Reason!.Trim(),
                LastUpdated = lastUpdated
            },
            cancellationToken);

        if (result.Outcome != SpeciesUpdateOutcome.Success)
        {
            logger.SaveFailed(Input.SpeciesId, result.Outcome);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not save this change. Try again later.");
            await RedisplayEditPanelAsync(cancellationToken);
            return Page();
        }

        logger.Saved(Input.SpeciesId);

        // Post-redirect-get: re-selects the species and shows the confirmation banner without
        // resubmitting the form on refresh.
        return RedirectToPage(new { species = Input.SpeciesId, saved = true });
    }

    /// <summary>Closes the "Edit name/parent" section without applying any change.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken)
    {
        await LoadTreeAsync(cancellationToken);

        ShowEditPanel = false;

        return Page();
    }

    private async Task OpenEditPanelAsync(SpeciesDetailDto detail, CancellationToken cancellationToken)
    {
        SpeciesDetail = detail;
        ValidParents = await speciesApiService.GetSpeciesValidParentsAsync(detail.Id, cancellationToken);
        ShowEditPanel = true;

        Input = new EditNameParentInput
        {
            SpeciesId = detail.Id,
            Name = detail.Name,
            ParentId = detail.ParentId == Guid.Empty ? null : detail.ParentId,
            LastUpdatedBase64 = Convert.ToBase64String(detail.LastUpdated)
        };
    }

    private async Task RedisplayEditPanelAsync(CancellationToken cancellationToken)
    {
        ShowEditPanel = true;
        SpeciesDetail = await speciesApiService.GetSpeciesDetailAsync(Input.SpeciesId, cancellationToken);
        ValidParents = await speciesApiService.GetSpeciesValidParentsAsync(Input.SpeciesId, cancellationToken);
    }

    private void ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError("Input.Name", "You need to provide a new name for this species.");
        }
        else if (Input.Name.Trim().Length > 50)
        {
            ModelState.AddModelError("Input.Name", "The name must be no longer than 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(Input.Reason))
        {
            ModelState.AddModelError("Input.Reason", "You need to provide a reason for this change.");
        }
        else if (Input.Reason.Trim().Length > 255)
        {
            ModelState.AddModelError("Input.Reason", "The reason for change must be no longer than 255 characters.");
        }
    }

    private async Task LoadTreeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = SpeciesKey,
                FieldName = SpeciesKey,
                ItemNameSingular = SpeciesKey,
                ItemNamePlural = SpeciesKey,
                SelectedValue = SelectedSpeciesId?.ToString(),
                Nodes = SpeciesTreeBuilder.Build(species)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SpeciesLoadFailed(exception);

            HasError = true;
            ErrorMessage = "We could not load species data. Try again later.";
        }
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = SpeciesKey,
        FieldName = SpeciesKey,
        ItemNameSingular = SpeciesKey,
        ItemNamePlural = SpeciesKey,
        Nodes = []
    };
}

/// <summary>The "Edit name/parent" form fields.</summary>
public sealed class EditNameParentInput
{
    /// <summary>Gets or sets the species being edited.</summary>
    public Guid SpeciesId { get; set; }

    /// <summary>Gets or sets the new display name.</summary>
    [Display(Name = "New name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the new parent identifier; <see langword="null"/> for a root species.</summary>
    [Display(Name = "New parent")]
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the reason given for the change.</summary>
    [Display(Name = "Reason for change")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the row version last read for this species, base64-encoded for the hidden field.</summary>
    public string LastUpdatedBase64 { get; set; } = string.Empty;
}

/// <summary>Source-generated structured log messages for <see cref="MaintainModel"/>.</summary>
internal static partial class MaintainLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Error, Message = "Failed to load species data from CDC.Api")]
    public static partial void SpeciesLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Failed to save name/parent change for species {SpeciesId}: {Outcome}")]
    public static partial void SaveFailed(this ILogger logger, Guid speciesId, SpeciesUpdateOutcome outcome);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Saved name/parent change for species {SpeciesId}")]
    public static partial void Saved(this ILogger logger, Guid speciesId);
}
