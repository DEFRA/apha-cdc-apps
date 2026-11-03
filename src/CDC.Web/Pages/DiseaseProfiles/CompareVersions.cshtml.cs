using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace CDC.Web.Pages.DiseaseProfiles;

/// <summary>
/// Compare profile versions: pick two versions from a profile hierarchy and download a PDF of
/// only the answers that differ. Replaces the legacy <c>CompareProfileVersions.aspx</c> and
/// <c>ShowProfileVersionComparisonReport.aspx</c>.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanViewProfileComparison)]
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

    /// <summary>
    /// Opened from the review reminder email: compares the profile's current draft with its current published
    /// version, as the legacy email attachment did, but generated when the link is followed.
    /// </summary>
    public async Task<IActionResult> OnGetReviewAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var versions = await LoadTreeAsync(cancellationToken);

        if (versions is null)
        {
            return Page();
        }

        ProfileAttributesDto? profile;

        try
        {
            profile = profileId == Guid.Empty ? null : await apiClient.GetProfileAttributesAsync(profileId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            LogLoadFailed(logger, exception);
            ErrorMessage = "Unable to retrieve the profile. The service may be temporarily unavailable.";

            return Page();
        }

        if (profile is null
            || !versions.TryGetValue(profile.CurrentPublishedProfileVersionId, out var published)
            || !versions.TryGetValue(profile.CurrentDraftProfileVersionId, out var draft))
        {
            ErrorMessage = "There is no draft and published version of this profile to compare. Select the versions to compare below.";

            return Page();
        }

        return await RenderReportAsync(published, draft, cancellationToken);
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

        return await RenderReportAsync(versions[ordered[0]], versions[ordered[1]], cancellationToken);
    }

    public IActionResult OnPostRemoveSelection() => RedirectToPage();

    private async Task<IActionResult> RenderReportAsync(ProfileVersionOption left, ProfileVersionOption right, CancellationToken cancellationToken)
    {
        try
        {
            var report = await comparisonService.BuildReportAsync(left, right, cancellationToken);
            var html = await viewRenderer.RenderAsync(ReportViewPath, report);
            var pdf = await pdfRenderer.RenderLandscapeAsync(html, cancellationToken);

            return File(pdf, "application/pdf", ReportFileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or PlaywrightException)
        {
            LogReportFailed(logger, left.VersionId, right.VersionId, exception);
            ErrorMessage = "The comparison report could not be generated. Try again later.";

            return Page();
        }
    }

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
