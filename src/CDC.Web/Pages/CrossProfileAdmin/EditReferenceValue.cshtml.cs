using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Changes the lookup value of a single reference value. The old value is shown for confirmation
/// and a reason for change is mandatory, so that every change reaches the audit trail explained.
/// As in the legacy spuReferenceValue, the effective date is stamped by the system rather than
/// entered by the user.
/// </summary>
/// <param name="referenceDataService">Reads and updates reference data.</param>
public class EditReferenceValueModel(IReferenceDataService referenceDataService)
    : BreadcrumbPageModelBase("Change a reference value")
{
    /// <summary>Maximum length accepted for a lookup value, matching [ReferenceValue].[LookupValue].</summary>
    public const int ValueMaxLength = 255;

    /// <summary>Maximum length accepted for a reason, matching [ReferenceTableAuditLog].[Reason].</summary>
    public const int ReasonMaxLength = 255;

    // Until Entra ID sign-in is wired up there is no authenticated principal to attribute changes
    // to, so the audit trail records this placeholder instead of an empty user.
    private const string UnknownUser = "Unknown user";

    /// <summary>Gets or sets the id of the table the value belongs to.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid TableId { get; set; }

    /// <summary>Gets or sets the identifier of the value being changed.</summary>
    [BindProperty(Name = "valueId", SupportsGet = true)]
    public Guid ValueId { get; set; }

    /// <summary>Gets the table the value belongs to.</summary>
    public ReferenceTableSummary? Table { get; private set; }

    /// <summary>Gets the value as it stands before the change.</summary>
    public ReferenceDataValue? ExistingValue { get; private set; }

    /// <summary>Gets or sets the replacement lookup value.</summary>
    [BindProperty]
    public string? NewLookupValue { get; set; }

    /// <summary>Gets or sets the reason the value is being changed. Required.</summary>
    [BindProperty]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the confirmation message handed to the reference data list page.</summary>
    [TempData]
    public string? ConfirmationMessage { get; set; }

    /// <summary>Loads the value and pre-fills the form with its current lookup value.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadValueAsync(cancellationToken))
        {
            return NotFound();
        }

        NewLookupValue = ExistingValue!.LookupValue;

        return Page();
    }

    /// <summary>Validates the change and, when valid, applies it and writes the audit entry.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadValueAsync(cancellationToken))
        {
            return NotFound();
        }

        ValidateInput();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var update = new ReferenceDataUpdate(
            Table!.Id,
            ExistingValue!.Id,
            NewLookupValue!.Trim(),
            Reason!.Trim(),
            User.Identity?.Name ?? UnknownUser);

        var result = await referenceDataService.UpdateValueAsync(update, cancellationToken);

        switch (result.Outcome)
        {
            case ReferenceDataUpdateOutcome.DuplicateLookupValue:
                ModelState.AddModelError(nameof(NewLookupValue), "There is already a reference value with this name");
                return Page();

            case ReferenceDataUpdateOutcome.ValueNotFound:
                return NotFound();

            default:
                ConfirmationMessage = $"{result.AuditEntry!.OldLookupValue} has been changed to {result.AuditEntry.NewLookupValue}.";
                return RedirectToPage("/CrossProfileAdmin/ReferenceData", new { tableId = Table.Id });
        }
    }

    /// <summary>
    /// Gets the first error message recorded against <paramref name="key"/>, or <see langword="null"/>
    /// if there is none. Used by the view to render the GOV.UK error summary and inline messages.
    /// </summary>
    public string? ErrorFor(string key) =>
        ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0
            ? entry.Errors[0].ErrorMessage
            : null;

    private async Task<bool> LoadValueAsync(CancellationToken cancellationToken)
    {
        if (TableId == Guid.Empty || ValueId == Guid.Empty)
        {
            return false;
        }

        Table = await referenceDataService.GetTableAsync(TableId, cancellationToken);

        if (Table is null)
        {
            return false;
        }

        ExistingValue = await referenceDataService.GetValueAsync(Table.Id, ValueId, cancellationToken);

        return ExistingValue is not null;
    }

    private void ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(NewLookupValue))
        {
            ModelState.AddModelError(nameof(NewLookupValue), "Enter a new value");
        }
        else if (NewLookupValue.Trim().Length > ValueMaxLength)
        {
            ModelState.AddModelError(nameof(NewLookupValue), $"New value must be {ValueMaxLength} characters or fewer");
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ModelState.AddModelError(nameof(Reason), "Enter a reason for change");
        }
        else if (Reason.Trim().Length > ReasonMaxLength)
        {
            ModelState.AddModelError(nameof(Reason), $"Reason for change must be {ReasonMaxLength} characters or fewer");
        }
    }
}
