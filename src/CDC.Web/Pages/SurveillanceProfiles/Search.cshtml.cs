using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Pages;

/// <summary>
/// Razor Page for searching disease surveillance profiles with comprehensive filtering options.
/// </summary>
public class SurveillanceProfilesSearchModel : PageModel
{
    private static readonly Action<ILogger, Exception?> LogFailedToLoadSpeciesFilterValuesMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogFailedToLoadSpeciesFilterValuesMessage)),
            "Failed to load species filter values");
    private static readonly Action<ILogger, Exception?> LogFailedToRetrieveProfileSearchResultsMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToRetrieveProfileSearchResultsMessage)),
            "Failed to retrieve profile search results");
    private static readonly Action<ILogger, Exception?> LogUnexpectedErrorWhileSearchingProfilesMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(LogUnexpectedErrorWhileSearchingProfilesMessage)),
            "An unexpected error occurred while searching profiles");
    private static readonly Action<ILogger, Exception?> LogSearchCompletedWithNoResultsMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4, nameof(LogSearchCompletedWithNoResultsMessage)),
            "Profile search completed with 0 results after applying filter selections");
    private static readonly Action<ILogger, int, Exception?> LogSearchCompletedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5, nameof(LogSearchCompletedMessage)),
            "Profile search completed with {ResultCount} results");

    private readonly IApiClient apiClient;
    private readonly ISpeciesApiService speciesApiService;
    private readonly ILogger<SurveillanceProfilesSearchModel> logger;

    public SurveillanceProfilesSearchModel(
        IApiClient apiClient,
        ISpeciesApiService speciesApiService,
        ILogger<SurveillanceProfilesSearchModel> logger)
    {
        this.apiClient = apiClient;
        this.speciesApiService = speciesApiService;
        this.logger = logger;
    }

    /// <summary>Gets or sets the search text filter.</summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchText { get; set; }

    /// <summary>Gets or sets whether to display published versions.</summary>
    [BindProperty(SupportsGet = true)]
    public bool DisplayPublished { get; set; } = true;

    /// <summary>Gets or sets whether to display draft versions.</summary>
    [BindProperty(SupportsGet = true)]
    public bool DisplayDraft { get; set; }

    /// <summary>Gets or sets whether to display scenario versions.</summary>
    [BindProperty(SupportsGet = true)]
    public bool DisplayScenarios { get; set; }

    /// <summary>Gets or sets the selected letter filter.</summary>
    [BindProperty(SupportsGet = true)]
    public string SelectedLetter { get; set; } = "All";

    /// <summary>Gets or sets the selected species identifiers from the shared tree picker. The
    /// Search page allows multiple species to be selected.</summary>
    [BindProperty(SupportsGet = true)]
    public List<string> SelectedSpecies { get; set; } = [];

    /// <summary>Gets or sets which part of a profile the search text is matched against.</summary>
    [BindProperty(SupportsGet = true)]
    public string AppearsIn { get; set; } = AppearsInOptions[0].Value;

    /// <summary>Gets or sets whether the search text is matched as one exact word or phrase.</summary>
    [BindProperty(SupportsGet = true)]
    public bool SearchForExactPhrase { get; set; } = true;

    /// <summary>Gets or sets whether the search text is matched as all of its individual words.</summary>
    [BindProperty(SupportsGet = true)]
    public bool SearchForAllWords { get; set; }

    /// <summary>Gets or sets how the results list is ordered.</summary>
    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = SortByOptions[0].Value;

    /// <summary>Gets or sets the number of results shown per page, or "All" for no paging.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets the static, hardcoded options for the <see cref="AppearsIn"/> dropdown.</summary>
    public static IReadOnlyList<AppearsInOption> AppearsInOptions { get; } =
    [
        new AppearsInOption("ProfileTitles", "Profile titles"),
        new AppearsInOption("All", "All"),
        new AppearsInOption("ProfileAnswers", "Profile answers"),
        new AppearsInOption("References", "References"),
        new AppearsInOption("FurtherInformation", "Further information")
    ];

    /// <summary>Gets the static, hardcoded options for the <see cref="SortBy"/> dropdown.</summary>
    public static IReadOnlyList<SortByOption> SortByOptions { get; } =
    [
        new SortByOption("Az", "A-Z"),
        new SortByOption("Za", "Z-A"),
        new SortByOption("MostRecentlyUpdated", "Most recently updated"),
        new SortByOption("LeastRecentlyUpdated", "Least recently updated")
    ];

    /// <summary>Gets the static, hardcoded options for the <see cref="PageSize"/> dropdown.</summary>
    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>Gets every result matching the current filters, before paging is applied.</summary>
    public IReadOnlyList<ProfileSearchResultDto> SearchResults { get; private set; } = [];

    /// <summary>Gets the single page of results to render.</summary>
    public IReadOnlyList<ProfileSearchResultDto> PagedResults { get; private set; } = [];

    /// <summary>Gets the total number of results matching the current filters.</summary>
    public int TotalResultCount { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets the species filter tree.</summary>
    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    /// <summary>Gets the display label for the currently selected species filter.</summary>
    public string SelectedSpeciesLabel { get; private set; } = AnySpeciesLabel;

    /// <summary>Gets or sets the error message if a search or species lookup fails.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets a value indicating whether the search has been performed.</summary>
    public bool IsSearchPerformed { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadSpeciesTreeAsync(cancellationToken);
        await PerformSearchAsync(cancellationToken);

        // The search filters (Display checkboxes, sort, page size/number, species tree) are
        // resubmitted via a background fetch rather than a full page reload; that request sends
        // this header so only the results fragment is rendered back, not the whole page.
        if (string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.Ordinal))
        {
            return Partial("_SearchResults", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadSpeciesTreeAsync(cancellationToken);
        await PerformSearchAsync(cancellationToken);
        return Page();
    }

    /// <summary>Builds the querystring URL for an alphabet quick-filter link, preserving every
    /// other filter, including the (possibly multi-valued) species selection.</summary>
    public string? BuildLetterUrl(string letter) => BuildSearchUrl(new
    {
        SearchText,
        DisplayPublished,
        DisplayDraft,
        DisplayScenarios,
        AppearsIn,
        SortBy,
        PageSize,
        SelectedLetter = letter
    });

    /// <summary>Builds the querystring URL for a results page link, preserving every other filter.</summary>
    public string? BuildPageUrl(int page) => BuildSearchUrl(new
    {
        SearchText,
        DisplayPublished,
        DisplayDraft,
        DisplayScenarios,
        AppearsIn,
        SortBy,
        PageSize,
        SelectedLetter,
        PageNumber = page
    });

    /// <summary>Gets the most relevant version to summarise for a profile: the latest published
    /// version, falling back to the latest draft, then the latest scenario.</summary>
    public static ProfileHistoryItemDto? GetCurrentVersion(ProfileSearchResultDto profile) =>
        profile.PublishedVersions.MaxBy(version => version.VersionNumber)
        ?? profile.DraftVersions.MaxBy(version => version.VersionNumber)
        ?? profile.Scenarios.MaxBy(version => version.VersionNumber);

    /// <summary>Gets the label to show alongside <see cref="GetCurrentVersion"/>'s result.</summary>
    public static string GetCurrentVersionLabel(ProfileSearchResultDto profile) => profile switch
    {
        { PublishedVersions.Count: > 0 } => "Published current version",
        { DraftVersions.Count: > 0 } => "Draft current version",
        { Scenarios.Count: > 0 } => "Scenario version",
        _ => "Version"
    };

    /// <summary>Gets every version other than the one <see cref="GetCurrentVersion"/> returns,
    /// newest first, for the "Show previous versions" toggle.</summary>
    public static IReadOnlyList<PreviousVersionRow> GetPreviousVersions(ProfileSearchResultDto profile)
    {
        var current = GetCurrentVersion(profile);

        IEnumerable<PreviousVersionRow> Labelled(IReadOnlyList<ProfileHistoryItemDto> versions, string status) =>
            versions.Select(version => new PreviousVersionRow(version, status));

        return
        [
            .. Labelled(profile.PublishedVersions, "Published")
                .Concat(Labelled(profile.DraftVersions, "Draft"))
                .Concat(Labelled(profile.Scenarios, "Scenario"))
                .Where(row => row.Version.VersionId != current?.VersionId)
                .OrderByDescending(row => row.Version.VersionNumber)
        ];
    }

    private string? BuildSearchUrl(object routeValues)
    {
        var url = Url.Page("/SurveillanceProfiles/Search", routeValues);

        if (url is null)
        {
            return null;
        }

        return SelectedSpecies.Aggregate(url, (current, speciesId) => QueryHelpers.AddQueryString(current, nameof(SelectedSpecies), speciesId));
    }

    private const string AnySpeciesLabel = "Any species";
    private const string SpeciesParameter = "species";

    private async Task LoadSpeciesTreeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = "species-filter",
                FieldName = nameof(SelectedSpecies),
                ItemNameSingular = SpeciesParameter,
                ItemNamePlural = SpeciesParameter,
                Nodes = SpeciesTreeBuilder.Build(species),
                EmptySelectionLabel = AnySpeciesLabel,
                AllowMultipleSelection = true,
                SelectedValues = SelectedSpecies.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            LogFailedToLoadSpeciesFilterValuesMessage(logger, exception);
            SpeciesTree = EmptyTree();
        }

        var labels = SelectedSpecies
            .Select(value => FindSpeciesLabel(SpeciesTree.Nodes, value))
            .Where(label => label is not null)
            .ToList();

        SelectedSpeciesLabel = labels.Count > 0 ? string.Join(", ", labels) : AnySpeciesLabel;
    }

    /// <summary>Recursively searches the tree for the node whose value matches <paramref name="value"/>.</summary>
    private static string? FindSpeciesLabel(IReadOnlyList<TreeNodeViewModel> nodes, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (string.Equals(node.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return node.Label;
            }

            var childMatch = FindSpeciesLabel(node.Children, value);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    /// <summary>Executes the profile search against the enriched search endpoint, then applies the
    /// letter filter, sort order and paging - none of which the API endpoint supports itself.</summary>
    private async Task PerformSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsSearchPerformed = true;

            if (!DisplayPublished && !DisplayDraft && !DisplayScenarios)
            {
                SearchResults = [];
                PagedResults = [];
                TotalResultCount = 0;
                TotalPages = 1;
                LogSearchCompletedWithNoResultsMessage(logger, null);
                return;
            }

            var response = await apiClient.SearchProfilesAsync(SearchText, DisplayPublished, DisplayDraft, DisplayScenarios, cancellationToken);
            var profiles = response.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SelectedLetter) && !string.Equals(SelectedLetter, "All", StringComparison.OrdinalIgnoreCase))
            {
                profiles = profiles.Where(item => item.Title.StartsWith(SelectedLetter, StringComparison.OrdinalIgnoreCase));
            }

            profiles = SortBy switch
            {
                "Za" => profiles.OrderByDescending(item => item.Title, StringComparer.OrdinalIgnoreCase),
                "MostRecentlyUpdated" => profiles.OrderByDescending(item => item.ModifiedAtUtc),
                "LeastRecentlyUpdated" => profiles.OrderBy(item => item.ModifiedAtUtc),
                _ => profiles.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            };

            SearchResults = profiles.ToList();
            TotalResultCount = SearchResults.Count;

            var pageSize = ResolvePageSize(PageSize);

            TotalPages = pageSize > 0 ? Math.Max(1, (int)Math.Ceiling(TotalResultCount / (double)pageSize)) : 1;
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

            PagedResults = pageSize > 0
                ? SearchResults.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToList()
                : SearchResults;

            LogSearchCompletedMessage(logger, TotalResultCount, null);
        }
        catch (HttpRequestException ex)
        {
            LogFailedToRetrieveProfileSearchResultsMessage(logger, ex);
            ErrorMessage = "Unable to retrieve profiles. The service may be temporarily unavailable.";
            SearchResults = [];
            PagedResults = [];
        }
        catch (Exception ex)
        {
            LogUnexpectedErrorWhileSearchingProfilesMessage(logger, ex);
            ErrorMessage = "An unexpected error occurred. Please try again.";
            SearchResults = [];
            PagedResults = [];
        }
    }

    /// <summary>Resolves the "Items per page" selection to a page size, where 0 means "All" (no paging).</summary>
    private static int ResolvePageSize(string pageSize)
    {
        if (string.Equals(pageSize, "All", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(pageSize, out var parsedPageSize) ? parsedPageSize : 10;
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = "species-filter",
        FieldName = nameof(SelectedSpecies),
        ItemNameSingular = SpeciesParameter,
        ItemNamePlural = SpeciesParameter,
        Nodes = []
    };
}

/// <summary>A previous version row for the "Show previous versions" panel, paired with the
/// bucket (Published/Draft/Scenario) it came from.</summary>
public sealed record PreviousVersionRow(ProfileHistoryItemDto Version, string Status);
