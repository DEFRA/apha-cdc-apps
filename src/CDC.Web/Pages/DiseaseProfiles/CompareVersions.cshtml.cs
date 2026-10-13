using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace CDC.Web.Pages.DiseaseProfiles;

/// <summary>
/// Compare profile versions: pick two versions from a profile hierarchy and download a PDF of
/// only the answers that differ. Replaces the legacy <c>CompareProfileVersions.aspx</c> and
/// <c>ShowProfileVersionComparisonReport.aspx</c>.
/// </summary>
public class CompareVersionsModel(
    IApiClient apiClient,
    IProfileVersionComparisonService comparisonService,
    IRazorViewRenderer viewRenderer,
    IPdfRenderer pdfRenderer,
    ILogger<CompareVersionsModel> logger) : BreadcrumbPageModelBase("Compare profile versions")
{
    private const string ReportViewPath = "~/Views/Reports/ProfileVersionComparisonReport.cshtml";
    private const string ReportFileName = "ProfileVersionComparison.pdf";
    private const string SelectionErrorText = "Select two profile versions to compare";

    private static readonly Action<ILogger, Exception?> LogLoadFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(LogLoadFailed)), "Failed to load profiles for comparison");

    private static readonly Action<ILogger, Guid, Guid, Exception?> LogReportFailed =
        LoggerMessage.Define<Guid, Guid>(LogLevel.Error, new EventId(2, nameof(LogReportFailed)), "Failed to generate comparison report for {LeftVersionId} and {RightVersionId}");

    /// <summary>Gets or sets the ticked version ids, in document order.</summary>
    [BindProperty]
    public List<string> SelectedVersions { get; set; } = [];

    /// <summary>Gets or sets the version ticked first (the earlier version), set by script.</summary>
    [BindProperty]
    public string? FirstVersionId { get; set; }

    /// <summary>Gets or sets the version ticked second (the later version), set by script.</summary>
    [BindProperty]
    public string? SecondVersionId { get; set; }

    /// <summary>Gets the profile version hierarchy.</summary>
    public TreeViewViewModel Tree { get; private set; } = new() { IdPrefix = "compare-version", FieldName = ProfileVersionTreeBuilder.FieldName, Nodes = [] };

    /// <summary>Gets the selected versions in the order they were chosen.</summary>
    public IReadOnlyList<string> SelectedLabels { get; private set; } = [];

    /// <summary>Gets the error shown in the error summary, if any.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets whether the error is about the selection rather than a service failure.</summary>
    public bool IsSelectionError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadTreeAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var versions = await LoadTreeAsync(cancellationToken);

        if (versions is null)
        {
            return Page();
        }

        var ordered = OrderSelection(versions);

        if (ordered.Count != 2)
        {
            ErrorMessage = SelectionErrorText;
            IsSelectionError = true;

            return Page();
        }

        try
        {
            var report = await comparisonService.BuildReportAsync(versions[ordered[0]], versions[ordered[1]], cancellationToken);
            var html = await viewRenderer.RenderAsync(ReportViewPath, report, cancellationToken);
            var pdf = await pdfRenderer.RenderLandscapeAsync(html, cancellationToken);

            return File(pdf, "application/pdf", ReportFileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or PlaywrightException)
        {
            LogReportFailed(logger, ordered[0], ordered[1], exception);
            ErrorMessage = "The comparison report could not be generated. Try again later.";

            return Page();
        }
    }

    public IActionResult OnPostRemoveSelection() => RedirectToPage();

    private async Task<IReadOnlyDictionary<Guid, ProfileVersionOption>?> LoadTreeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profiles = await apiClient.SearchProfilesAsync(
                searchText: null,
                displayPublished: true,
                displayDraft: true,
                displayScenarios: true,
                SearchForType.ExactWordOrPhrase,
                cancellationToken);

            var posted = SelectedVersions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = ProfileVersionTreeBuilder.Build(profiles, posted);
            Tree = result.Tree;

            SelectedLabels = [.. OrderSelection(result.Versions).Select(id => result.Versions[id].FullLabel)];

            return result.Versions;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            LogLoadFailed(logger, exception);
            ErrorMessage = "Unable to retrieve profiles. The service may be temporarily unavailable.";

            return null;
        }
    }

    // Only ids that exist in the tree count; the scripted first/second order is honoured when it
    // matches the ticked set, otherwise the order the browser posted them in is used.
    private List<Guid> OrderSelection(IReadOnlyDictionary<Guid, ProfileVersionOption> versions)
    {
        var posted = SelectedVersions
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(versions.ContainsKey)
            .Distinct()
            .ToList();

        if (posted.Count == 2
            && Guid.TryParse(FirstVersionId, out var first)
            && Guid.TryParse(SecondVersionId, out var second)
            && posted.Contains(first)
            && posted.Contains(second)
            && first != second)
        {
            return [first, second];
        }

        return posted;
    }
}
