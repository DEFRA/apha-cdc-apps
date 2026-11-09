using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Lists the values held in a selected maintainable reference table and, as the legacy
/// MaintainReferenceData page did, shows a section beneath the list to edit, add, inactivate or
/// delete a value. A reason for change is mandatory so every change reaches the audit trail
/// explained; effective dates are stamped by the system, as in spuReferenceValue.
/// </summary>
/// <param name="referenceDataService">Reads and maintains reference tables and their values.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanManageReferenceData)]
public class ReferenceDataModel(IReferenceDataApiService referenceDataService, ILogger<ReferenceDataModel> logger)
    : BreadcrumbPageModelBase("Maintain reference data")
{
    /// <summary>Maximum length accepted for a lookup value, matching [ReferenceValue].[LookupValue].</summary>
    public const int ValueMaxLength = 255;

    /// <summary>Maximum length accepted for a reason, matching [ReferenceTableAuditLog].[Reason].</summary>
    public const int ReasonMaxLength = 255;

    /// <summary>The confirmation shown after any successful change, as in legacy.</summary>
    public const string SuccessMessage = "Reference value change successfully saved";

    /// <summary>The id of the change section heading, which receives focus when the section opens.</summary>
    public const string PanelHeadingId = "reference-value-panel-title";

    private const string PageRoute = "/CrossProfileAdmin/ReferenceData";
    private const string Ascending = "Ascending";
    private const string Descending = "Descending";

    // Until Entra ID sign-in is wired up there is no authenticated principal to attribute changes
    // to, so the audit trail records this placeholder instead of an empty user.
    private const string UnknownUser = "Unknown user";

    /// <summary>Gets or sets the id of the table chosen in the dropdown.</summary>
    [BindProperty(Name = "tableId", SupportsGet = true)]
    public Guid? SelectedTableId { get; set; }

    /// <summary>Gets the maintainable reference tables offered in the dropdown.</summary>
    public IReadOnlyList<ReferenceTableSummary> Tables { get; private set; } = [];

    /// <summary>Gets the table matching <see cref="SelectedTableId"/>, if one was selected.</summary>
    public ReferenceTableSummary? SelectedTable { get; private set; }

    /// <summary>Gets every value belonging to <see cref="SelectedTable"/>.</summary>
    public IReadOnlyList<ReferenceDataValue> Values { get; private set; } = [];

    /// <summary>Gets the page of <see cref="Values"/> to display.</summary>
    public IReadOnlyList<ReferenceDataValue> PagedValues { get; private set; } = [];

    /// <summary>Gets or sets the page of values shown, counting from 1.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the "Items per page" choice.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = ReferenceDataPaging.PageSizeOptions[0];

    /// <summary>Gets the number of pages of values at the current page size.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets or sets the column the values are sorted by. Legacy defaulted to the value.</summary>
    [BindProperty(SupportsGet = true)]
    public ReferenceValueSortColumn SortColumn { get; set; } = ReferenceValueSortColumn.Value;

    /// <summary>Gets or sets the sort direction: "Ascending" or "Descending".</summary>
    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = Ascending;

    /// <summary>Gets a value indicating whether the values are sorted in descending order.</summary>
    public bool IsSortDescending => string.Equals(SortDirection, Descending, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets or sets the kind of change open in the section beneath the list, if any.</summary>
    [BindProperty(Name = "mode", SupportsGet = true)]
    public ReferenceDataChangeAction? Mode { get; set; }

    /// <summary>Gets or sets the value being changed. Not used when adding.</summary>
    [BindProperty(Name = "valueId", SupportsGet = true)]
    public Guid? ValueId { get; set; }

    /// <summary>Gets the value as it stands before the change. <see langword="null"/> when adding.</summary>
    public ReferenceDataValue? ExistingValue { get; private set; }

    /// <summary>Gets or sets the replacement lookup value, which may carry italics.</summary>
    [BindProperty]
    public string? NewLookupValue { get; set; }

    /// <summary>Gets or sets the reason the value is being changed, which may carry links. Required.</summary>
    [BindProperty]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the row version read when the change was opened, base64-encoded, so a concurrent edit is detected.</summary>
    [BindProperty]
    public string? LastUpdated { get; set; }

    /// <summary>Gets the row version to post back with the change.</summary>
    public string? RowVersion => LastUpdated ?? (ExistingValue?.LastUpdated is { } lastUpdated ? Convert.ToBase64String(lastUpdated) : null);

    /// <summary>Gets or sets the success message shown after a change was saved.</summary>
    [TempData]
    public string? ConfirmationMessage { get; set; }

    /// <summary>Gets a value indicating whether the change section is shown.</summary>
    public bool ShowPanel => Mode is not null && SelectedTable is not null;

    /// <summary>Gets the change section heading, as legacy: the action, then "reference data value".</summary>
    public string PanelHeading => $"{Mode ?? ReferenceDataChangeAction.Edit} reference data value";

    /// <summary>Gets the old value shown for confirmation.</summary>
    public string OldValueText => Mode == ReferenceDataChangeAction.Add
        ? ReferenceDataPlaceholders.NewEntry
        : ExistingValue?.LookupValue ?? string.Empty;

    /// <summary>Gets the fixed new value shown when inactivating or deleting, or <see langword="null"/> when the user enters one.</summary>
    public string? FixedNewValue => Mode switch
    {
        ReferenceDataChangeAction.Inactivate => ReferenceDataPlaceholders.ToBeInactivated,
        ReferenceDataChangeAction.Delete => ReferenceDataPlaceholders.ToBeDeleted,
        _ => null
    };

    /// <summary>Gets a value indicating whether existing profiles reference the value being changed.</summary>
    public bool ShowInUseWarning => Mode != ReferenceDataChangeAction.Add && ExistingValue?.IsInUse == true;

    /// <summary>Gets the text of the submit button.</summary>
    public string SubmitText => Mode is ReferenceDataChangeAction.Inactivate or ReferenceDataChangeAction.Delete
        ? Mode.Value.ToString()
        : "Save";

    /// <summary>Gets the error that is not about one field, such as a concurrent edit.</summary>
    public string? GeneralError => ErrorFor(string.Empty);

    /// <summary>Gets a value indicating whether the change failed validation.</summary>
    public bool HasErrors => ErrorFor(nameof(NewLookupValue)) is not null || ErrorFor(nameof(Reason)) is not null || GeneralError is not null;

    /// <summary>Gets the message shown when the reference data could not be loaded from CDC.Api.</summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>Loads the list and, when a change was chosen, opens the section beneath it.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadListAsync(cancellationToken);

            return Mode is null || await LoadChangeAsync(cancellationToken) ? Page() : NotFound();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ShowLoadError(exception);
            return Page();
        }
    }

    /// <summary>Validates the change and, when valid, applies it and writes the audit entry.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangeAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ShowLoadError(exception);
            return Page();
        }
    }

    private void ShowLoadError(Exception exception)
    {
        logger.ReferenceDataApiFailed(exception);
        Mode = null;
        LoadErrorMessage = "We could not load or save reference data. Try again later.";
    }

    private async Task<IActionResult> SaveChangeAsync(CancellationToken cancellationToken)
    {
        await LoadListAsync(cancellationToken);

        if (Mode is null || !await LoadChangeAsync(cancellationToken))
        {
            return NotFound();
        }

        var newValue = FixedNewValue ?? SpeciesNameHtml.Clean(NewLookupValue);
        var reason = Reason?.Trim() ?? string.Empty;

        ValidateInput(newValue, reason);

        if (HasErrors)
        {
            return Page();
        }

        var result = await referenceDataService.UpdateValueAsync(
            new ReferenceDataUpdate(
                SelectedTable!.Id,
                ExistingValue?.Id ?? Guid.Empty,
                newValue,
                reason,
                User.Identity?.Name ?? UnknownUser,
                Mode.Value,
                PostedRowVersion() ?? ExistingValue?.LastUpdated),
            cancellationToken);

        switch (result.Outcome)
        {
            case ReferenceDataUpdateOutcome.Updated:
                ConfirmationMessage = SuccessMessage;
                return RedirectToPage(PageRoute, BuildListRouteValues(PageNumber));

            case ReferenceDataUpdateOutcome.DuplicateLookupValue:
                ModelState.AddModelError(nameof(NewLookupValue), "There is already a reference value with this name");
                return Page();

            case ReferenceDataUpdateOutcome.Conflict:
                ModelState.AddModelError(string.Empty, "Another user has changed this reference value since you opened it. Cancel and try again.");
                return Page();

            default:
                return NotFound();
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

    /// <summary>Builds the URL of a page of values, keeping the selected table, page size and sort.</summary>
    public string? BuildPageUrl(int pageNumber) => Url.Page(PageRoute, BuildListRouteValues(pageNumber));

    /// <summary>Builds the URL that opens the change section for a value, keeping the list as it is.</summary>
    public string? BuildChangeUrl(ReferenceDataChangeAction mode, Guid? valueId = null)
    {
        var values = BuildListRouteValues(PageNumber);
        values["mode"] = mode;
        values["valueId"] = valueId;

        return Url.Page(PageRoute, values);
    }

    /// <summary>Builds the URL that closes the change section, keeping the list as it is.</summary>
    public string? BuildCancelUrl() => Url.Page(PageRoute, BuildListRouteValues(PageNumber));

    /// <summary>Builds the URL for a sortable column header: ascending for a new column, otherwise toggling the direction.</summary>
    public string? BuildSortUrl(ReferenceValueSortColumn column)
    {
        var values = BuildListRouteValues(1);
        values[nameof(SortColumn)] = column;
        values[nameof(SortDirection)] = column == SortColumn && !IsSortDescending ? Descending : Ascending;

        return Url.Page(PageRoute, values);
    }

    /// <summary>Gets the <c>aria-sort</c> value for a column header.</summary>
    public string GetAriaSort(ReferenceValueSortColumn column) =>
        column != SortColumn ? "none" : IsSortDescending ? "descending" : "ascending";

    private Dictionary<string, object?> BuildListRouteValues(int pageNumber) => new()
    {
        ["tableId"] = SelectedTableId,
        [nameof(PageNumber)] = pageNumber,
        [nameof(PageSize)] = PageSize,
        [nameof(SortColumn)] = SortColumn,
        [nameof(SortDirection)] = SortDirection
    };

    private byte[]? PostedRowVersion()
    {
        try
        {
            return string.IsNullOrEmpty(LastUpdated) ? null : Convert.FromBase64String(LastUpdated);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task LoadListAsync(CancellationToken cancellationToken)
    {
        Tables = await referenceDataService.GetTablesAsync(cancellationToken);

        if (SelectedTableId is not { } selectedTableId)
        {
            return;
        }

        SelectedTable = Tables.FirstOrDefault(table => table.Id == selectedTableId);
        SelectedTableId = SelectedTable?.Id;

        if (SelectedTable is not null)
        {
            Values = Sort(await referenceDataService.GetValuesAsync(SelectedTable.Id, cancellationToken));
            (PagedValues, PageNumber, TotalPages) = ReferenceDataPaging.GetPage(Values, PageNumber, PageSize);
        }
    }

    private async Task<bool> LoadChangeAsync(CancellationToken cancellationToken)
    {
        if (SelectedTable is null || Mode is not { } mode || !Enum.IsDefined(mode) || ErrorFor("mode") is not null)
        {
            return false;
        }

        if (mode == ReferenceDataChangeAction.Add)
        {
            return true;
        }

        if (ValueId is not { } valueId)
        {
            return false;
        }

        ExistingValue = await referenceDataService.GetValueAsync(SelectedTable.Id, valueId, cancellationToken);

        // Inactive values cannot be changed, and only values no profile references can be deleted.
        return ExistingValue is { IsActive: true } &&
               !(mode == ReferenceDataChangeAction.Delete && ExistingValue.IsInUse);
    }

    private void ValidateInput(string newValue, string reason)
    {
        if (FixedNewValue is null)
        {
            if (ProfileTitleHtmlFormatter.ToPlainText(newValue).Length == 0)
            {
                ModelState.AddModelError(nameof(NewLookupValue), "You need to provide a new value");
            }
            else if (newValue.Length > ValueMaxLength)
            {
                ModelState.AddModelError(nameof(NewLookupValue), $"New value must be {ValueMaxLength} characters or fewer");
            }
        }

        if (ProfileTitleHtmlFormatter.ToPlainText(reason).Length == 0)
        {
            ModelState.AddModelError(nameof(Reason), "You need to provide a reason for this change");
        }
        else if (reason.Length > ReasonMaxLength)
        {
            ModelState.AddModelError(nameof(Reason), $"Reason for change must be {ReasonMaxLength} characters or fewer");
        }
    }

    private List<ReferenceDataValue> Sort(IReadOnlyList<ReferenceDataValue> values)
    {
        // Ties fall back to the table's sequence order, the order the service returns.
        var indexed = values.Select((value, index) => (value, index, text: ProfileTitleHtmlFormatter.ToPlainText(value.LookupValue)));

        var sorted = (SortColumn, IsSortDescending) switch
        {
            (ReferenceValueSortColumn.EffectiveDates, false) => indexed.OrderBy(item => item.value.EffectiveDateFrom),
            (ReferenceValueSortColumn.EffectiveDates, true) => indexed.OrderByDescending(item => item.value.EffectiveDateFrom),
            (_, false) => indexed.OrderBy(item => item.text, StringComparer.OrdinalIgnoreCase),
            _ => indexed.OrderByDescending(item => item.text, StringComparer.OrdinalIgnoreCase)
        };

        return [.. sorted.ThenBy(item => item.index).Select(item => item.value)];
    }
}

/// <summary>The sortable columns of the reference values table.</summary>
public enum ReferenceValueSortColumn
{
    /// <summary>Sort by the lookup value.</summary>
    Value,

    /// <summary>Sort by the effective-from date.</summary>
    EffectiveDates
}

/// <summary>Source-generated log messages for the reference data pages.</summary>
internal static partial class ReferenceDataPageLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Reference data could not be loaded or saved through CDC.Api")]
    public static partial void ReferenceDataApiFailed(this ILogger logger, Exception exception);
}
