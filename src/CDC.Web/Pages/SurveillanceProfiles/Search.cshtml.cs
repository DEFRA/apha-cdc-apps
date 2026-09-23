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
    private readonly HttpClient httpClient;
    private readonly ISpeciesApiService speciesApiService;
    private readonly ILogger<SurveillanceProfilesSearchModel> logger;

    public SurveillanceProfilesSearchModel(
        HttpClient httpClient,
        ISpeciesApiService speciesApiService,
        ILogger<SurveillanceProfilesSearchModel> logger)
    {
        this.httpClient = httpClient;
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

    /// <summary>Gets the static, hardcoded options for the <see cref="AppearsIn"/> dropdown.</summary>
    public static IReadOnlyList<AppearsInOption> AppearsInOptions { get; } =
    [
        new AppearsInOption("ProfileTitles", "Profile titles"),
        new AppearsInOption("All", "All"),
        new AppearsInOption("ProfileAnswers", "Profile answers"),
        new AppearsInOption("References", "References"),
        new AppearsInOption("FurtherInformation", "Further information")
    ];

    /// <summary>Gets the search results returned from the profile list endpoint.</summary>
    public IReadOnlyList<ProfileSummaryItemDto> SearchResults { get; private set; } = [];

    /// <summary>Gets the species filter tree.</summary>
    public TreeViewViewModel SpeciesTree { get; private set; } = EmptyTree();

    /// <summary>Gets the display label for the currently selected species filter.</summary>
    public string SelectedSpeciesLabel { get; private set; } = AnySpeciesLabel;

    /// <summary>Gets or sets the error message if a search or species lookup fails.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets a value indicating whether the search has been performed.</summary>
    public bool IsSearchPerformed { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadSpeciesTreeAsync(cancellationToken);
        await PerformSearchAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadSpeciesTreeAsync(cancellationToken);
        await PerformSearchAsync();
        return Page();
    }

    /// <summary>Builds the querystring URL for an alphabet quick-filter link, preserving every
    /// other filter, including the (possibly multi-valued) species selection.</summary>
    public string? BuildLetterUrl(string letter)
    {
        var url = Url.Page("/SurveillanceProfiles/Search", new
        {
            SearchText,
            DisplayPublished,
            DisplayDraft,
            DisplayScenarios,
            AppearsIn,
            SelectedLetter = letter
        });

        if (url is null)
        {
            return null;
        }

        return SelectedSpecies.Aggregate(url, (current, speciesId) => QueryHelpers.AddQueryString(current, nameof(SelectedSpecies), speciesId));
    }

    private const string AnySpeciesLabel = "Any species";

    private async Task LoadSpeciesTreeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var species = await speciesApiService.GetAllSpeciesAsync(cancellationToken);

            SpeciesTree = new TreeViewViewModel
            {
                IdPrefix = "species-filter",
                FieldName = nameof(SelectedSpecies),
                ItemNameSingular = "species",
                ItemNamePlural = "species",
                Nodes = SpeciesTreeBuilder.Build(species),
                EmptySelectionLabel = AnySpeciesLabel,
                AllowMultipleSelection = true,
                SelectedValues = SelectedSpecies.ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.LogError(exception, "Failed to load species filter values");
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

    /// <summary>Executes the profile search using the actual API response that Swagger has validated.</summary>
    private async Task PerformSearchAsync()
    {
        try
        {
            IsSearchPerformed = true;

            var response = await httpClient.GetFromJsonAsync<List<ProfileSummaryItemDto>>("/api/profile-search/profiles");
            var profiles = (response ?? []).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                profiles = profiles.Where(item => item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SelectedLetter) && !string.Equals(SelectedLetter, "All", StringComparison.OrdinalIgnoreCase))
            {
                profiles = profiles.Where(item => item.Name.StartsWith(SelectedLetter, StringComparison.OrdinalIgnoreCase));
            }

            var includePublished = DisplayPublished;
            var includeDraft = DisplayDraft;
            var includeScenario = DisplayScenarios;

            if (!includePublished && !includeDraft && !includeScenario)
            {
                SearchResults = [];
                logger.LogInformation("Profile search completed with 0 results after applying filter selections");
                return;
            }

            profiles = profiles.Where(item =>
                (includePublished && string.Equals(item.Status, "Published", StringComparison.OrdinalIgnoreCase)) ||
                (includeDraft && string.Equals(item.Status, "Draft", StringComparison.OrdinalIgnoreCase)) ||
                (includeScenario && string.Equals(item.Status, "Scenario", StringComparison.OrdinalIgnoreCase)));

            SearchResults = profiles.ToList();

            logger.LogInformation("Profile search completed with {ResultCount} results", SearchResults.Count);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to retrieve profile search results");
            ErrorMessage = "Unable to retrieve profiles. The service may be temporarily unavailable.";
            SearchResults = [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while searching profiles");
            ErrorMessage = "An unexpected error occurred. Please try again.";
            SearchResults = [];
        }
    }

    private static TreeViewViewModel EmptyTree() => new()
    {
        IdPrefix = "species-filter",
        FieldName = nameof(SelectedSpecies),
        ItemNameSingular = "species",
        ItemNamePlural = "species",
        Nodes = []
    };
}

/// <summary>
/// DTO matching the actual response returned by the Swagger-validated profile list endpoint.
/// </summary>
public record ProfileSummaryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
