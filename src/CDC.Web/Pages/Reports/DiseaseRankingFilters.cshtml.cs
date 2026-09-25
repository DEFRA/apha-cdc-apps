using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.Reports;

/// <summary>
/// Lets a user create, select, update and delete a disease ranking filter: a species (or
/// species group) plus a set of disease characteristics used to narrow the disease ranking
/// report.
/// </summary>
public class DiseaseRankingFiltersModel(
    ISpeciesApiService speciesApiService,
    IDiseaseRankingFilterStore filterStore,
    ILogger<DiseaseRankingFiltersModel> logger)
    : BreadcrumbPageModelBase("Create and maintain disease ranking filters")
{
    private const string SpeciesKey = "species";

    [BindProperty]
    public DiseaseRankingFilterInput Input { get; set; } = new();

    /// <summary>Every saved filter, for the "Select filter" list.</summary>
    public IReadOnlyList<DiseaseRankingFilterSummary> Filters { get; private set; } = [];

    /// <summary>Whether the species/criteria form (shown after "Create new filter" or selecting a filter) is visible.</summary>
    public bool ShowFilterForm { get; private set; }

    /// <summary>True once a filter has just been saved, so the confirmation banner is shown.</summary>
    public bool ShowSavedConfirmation { get; private set; }

    /// <summary>True once a filter has just been deleted, so the confirmation banner is shown.</summary>
    public bool ShowDeletedConfirmation { get; private set; }

    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    public bool HasSpeciesLoadError { get; private set; }

    public IReadOnlyList<(string FieldId, string Message)> Errors { get; private set; } = [];

    public async Task OnGetAsync(Guid? filterId, bool created, bool saved, bool deleted, CancellationToken cancellationToken)
    {
        Filters = filterStore.GetAll();
        ShowSavedConfirmation = saved;
        ShowDeletedConfirmation = deleted;

        if (created)
        {
            ShowFilterForm = true;
            Input = new DiseaseRankingFilterInput();
            await LoadSpeciesTreeAsync(selectedSpeciesId: null, cancellationToken);
        }
        else if (filterId is { } id && filterStore.TryGet(id, out var filter))
        {
            ShowFilterForm = true;
            Input = DiseaseRankingFilterInput.FromFilter(filter);
            await LoadSpeciesTreeAsync(filter.SpeciesId, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Filters = filterStore.GetAll();
        ShowFilterForm = true;
        Errors = Input.Validate();

        if (Errors.Count > 0)
        {
            await LoadSpeciesTreeAsync(Input.SpeciesId, cancellationToken);

            return Page();
        }

        var id = Input.Id ?? Guid.NewGuid();
        filterStore.Save(Input.ToFilter(id));

        return RedirectToPage(new { filterId = id, saved = true });
    }

    public IActionResult OnPostDelete()
    {
        if (Input.Id is { } id)
        {
            filterStore.Delete(id);
            logger.DiseaseRankingFilterDeleted(id);
        }

        return RedirectToPage(new { deleted = true });
    }

    private async Task LoadSpeciesTreeAsync(Guid? selectedSpeciesId, CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = SpeciesKey,
                FieldName = nameof(DiseaseRankingFilterInput.SpeciesId),
                ItemNameSingular = SpeciesKey,
                ItemNamePlural = SpeciesKey,
                Nodes = SpeciesTreeBuilder.Build(species, selectedSpeciesId),
                SelectedValue = selectedSpeciesId?.ToString()
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SpeciesLoadFailed(exception);
            HasSpeciesLoadError = true;
        }
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = SpeciesKey,
        FieldName = nameof(DiseaseRankingFilterInput.SpeciesId),
        Nodes = []
    };
}

/// <summary>Source-generated structured log messages for <see cref="DiseaseRankingFiltersModel"/>.</summary>
internal static partial class DiseaseRankingFiltersLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Error, Message = "Failed to load species data from CDC.Api")]
    public static partial void SpeciesLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Deleted disease ranking filter {FilterId}")]
    public static partial void DiseaseRankingFilterDeleted(this ILogger logger, Guid filterId);
}
