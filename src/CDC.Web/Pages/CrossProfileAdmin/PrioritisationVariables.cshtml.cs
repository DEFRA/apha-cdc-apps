using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>One criterion value's score, bound from the criterion edit panel's inputs.</summary>
public sealed class CriterionValueScoreInput
{
    /// <summary>Gets or sets the criterion value identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the score.</summary>
    public int Score { get; set; }
}

/// <summary>A criterion edit confirmed with the panel's OK button but not yet saved by Update.</summary>
public sealed class PendingCriterionChange
{
    /// <summary>Gets or sets the criterion identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the new name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the new weighting.</summary>
    public int Weight { get; set; }

    /// <summary>Gets or sets the new score of each value.</summary>
    public List<CriterionValueScoreInput> ValueScores { get; set; } = [];
}

// Mirrors the legacy MaintainPrioritisationVariables.aspx/.vb page: a separate "Go" action for
// draft-profile prioritisation; criterion edits confirmed with the panel's OK button and held as
// pending changes; and a single "Update" action that saves the ranking range and every pending
// criterion change together, then recalculates published profile scores.
[Authorize(Policy = AuthorizationPolicies.CanManagePrioritisationVariables)]
public class PrioritisationVariablesModel(
    IPrioritisationVariablesApiService prioritisationVariablesApiService,
    ILogger<PrioritisationVariablesModel> logger)
    : BreadcrumbPageModelBase("Maintain prioritisation variables")
{
    /// <summary>The model state key for a draft prioritisation failure, linked to the Go button.</summary>
    public const string DraftPrioritisationErrorKey = "DraftPrioritisation";

    /// <summary>The model state key for a CSV export failure, linked to the export buttons.</summary>
    public const string ExportErrorKey = "Export";

    private const string CsvContentType = "text/csv";

    private const int MaxCriterionNameLength = 255;

    private static readonly JsonSerializerOptions PendingChangesJson = new(JsonSerializerDefaults.Web);

    /// <summary>The upper bound shown when the page first loads, whatever is stored.</summary>
    public const int DefaultUpperBound = 90;

    [BindProperty]
    [Required(ErrorMessage = "Lower bound cannot be blank")]
    [Range(0, 998, ErrorMessage = "Lower bound must be a positive integer")]
    public int LowerBound { get; set; } = 0;

    [BindProperty]
    [Required(ErrorMessage = "Upper bound cannot be blank")]
    [Range(1, 999, ErrorMessage = "Upper bound must be a positive integer")]
    public int UpperBound { get; set; } = DefaultUpperBound;

    /// <summary>The ranking range's concurrency token, round-tripped via a hidden field so a
    /// stale save can be detected - mirrors the legacy page's <c>@LastUpdated</c> check.</summary>
    [BindProperty]
    public string RowVersion { get; set; } = string.Empty;

    /// <summary>The category chosen from the "Prioritisation category" dropdown, via the query string.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? SelectedCategoryId { get; set; }

    /// <summary>The criterion chosen from the criteria list, via the query string.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? SelectedCriterionId { get; set; }

    /// <summary>The weighting shown in the criterion edit panel.</summary>
    public int CriterionWeight { get; private set; }

    /// <summary>The criterion name shown in the criterion edit panel; may carry italics, as the legacy single-line editor allowed.</summary>
    public string? CriterionName { get; private set; }

    /// <summary>The per-value scores shown in the criterion edit panel.</summary>
    public List<CriterionValueScoreInput> CriterionValueScores { get; private set; } = [];

    /// <summary>The pending criterion changes as JSON, kept in a hidden field until Update saves them (the legacy page kept them in session).</summary>
    [BindProperty]
    public string? PendingChanges { get; set; }

    /// <summary>The pending criterion changes read from <see cref="PendingChanges"/>.</summary>
    public IReadOnlyList<PendingCriterionChange> PendingCriteria { get; private set; } = [];

    /// <summary>Gets the pending change for a criterion, if any.</summary>
    public PendingCriterionChange? PendingFor(Guid criterionId) =>
        PendingCriteria.FirstOrDefault(change => change.Id == criterionId);

    /// <summary>Gets the pending changes to write back into the hidden field.</summary>
    public string SerialisedPendingChanges =>
        PendingCriteria.Count == 0 ? string.Empty : JsonSerializer.Serialize(PendingCriteria, PendingChangesJson);

    public string? SuccessMessage { get; private set; }

    public string? UpdateResultMessage { get; private set; }

    public bool PublishedProfileScoresRecalculated { get; private set; }

    /// <summary>Every prioritisation category, for the dropdown.</summary>
    public IReadOnlyList<PrioritisationCategoryDto> Categories { get; private set; } = [];

    /// <summary>The criteria for <see cref="SelectedCategoryId"/> - the category-scoped "search results" grid.</summary>
    public IReadOnlyList<PrioritisationCriterionDto> SelectedCategoryCriteria { get; private set; } = [];

    /// <summary>The criterion matching <see cref="SelectedCriterionId"/>, if any.</summary>
    public PrioritisationCriterionDto? SelectedCriterion { get; private set; }

    /// <summary>Gets the message to show when the prioritisation categories could not be loaded.</summary>
    public string? CategoriesErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadRankingRangeAsync(cancellationToken);
        UpperBound = DefaultUpperBound;
        await LoadPrioritisationDataAsync(cancellationToken);
        PopulateCriterionInputs();
    }

    /// <summary>Returns just the selected category's actions and criteria, so choosing a category doesn't reload the page.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The category section.</returns>
    public async Task<IActionResult> OnGetCategorySectionAsync(CancellationToken cancellationToken)
    {
        await LoadPrioritisationDataAsync(cancellationToken);

        return Partial("_CategorySection", this);
    }

    /// <summary>Returns just the criterion edit panel, so choosing a criterion doesn't reload the whole page.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The panel, or not found when the criterion isn't in the selected category.</returns>
    public async Task<IActionResult> OnGetCriterionPanelAsync(CancellationToken cancellationToken)
    {
        await LoadPrioritisationDataAsync(cancellationToken);
        PopulateCriterionInputs();

        return SelectedCriterion is null ? NotFound() : Partial("_CriterionPanel", this);
    }

    private void PopulateCriterionInputs()
    {
        if (SelectedCriterion is not null)
        {
            CriterionWeight = SelectedCriterion.Weight;
            CriterionName = SelectedCriterion.Name;
            CriterionValueScores = [.. SelectedCriterion.Values.Select(value => new CriterionValueScoreInput { Id = value.Id, Score = value.Score })];
        }
    }

    public async Task<PageResult> OnPostRunDraftPrioritisation(CancellationToken cancellationToken)
    {
        // Go posts the update form so the on-screen range and pending criterion changes come back as they were;
        // they are only validated by Update, so their binding errors are cleared here.
        ModelState.Clear();
        ReadPendingChanges();

        UpdateResultMessage = null;
        PublishedProfileScoresRecalculated = false;

        try
        {
            await prioritisationVariablesApiService.PrioritiseDraftProfilesAsync(cancellationToken);
            SuccessMessage = "Prioritisation of draft profiles was successful";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.DraftPrioritisationFailed(exception);
            SuccessMessage = null;
            ModelState.AddModelError(DraftPrioritisationErrorKey, "Prioritisation of draft profiles failed - try again later");
        }

        await LoadPrioritisationDataAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// The single "Update" action: saves the ranking range and every pending criterion change
    /// together, then recalculates published profile scores, as the legacy Update button did.
    /// </summary>
    public async Task<PageResult> OnPostUpdate(CancellationToken cancellationToken)
    {
        SuccessMessage = null;

        ReadPendingChanges();
        await LoadPrioritisationDataAsync(cancellationToken);
        ValidateRangeAndPendingCriteria();

        if (!ModelState.IsValid)
        {
            UpdateResultMessage = null;
            PublishedProfileScoresRecalculated = false;
            return Page();
        }

        try
        {
            var updatedRange = await prioritisationVariablesApiService.UpdateRankingRangeAsync(
                new PrioritisationRankingRangeDto { LowerBound = LowerBound, UpperBound = UpperBound, RowVersion = RowVersion },
                cancellationToken);
            RowVersion = updatedRange.RowVersion;

            foreach (var change in PendingCriteria)
            {
                var valueScores = change.ValueScores
                    .Select(value => new CriterionValueScore { ValueId = value.Id, Score = value.Score })
                    .ToList();

                await prioritisationVariablesApiService.UpdateCriterionAsync(change.Id, change.Weight, valueScores, change.Name, cancellationToken);
            }

            // The legacy save ran sppPrioritisationScore so published profiles never kept scores from the old variables.
            await prioritisationVariablesApiService.RecalculateScoresAsync(cancellationToken);

            PendingCriteria = [];
            PublishedProfileScoresRecalculated = true;
            UpdateResultMessage = "Your changes were successfully saved";
        }
        catch (RankingRangeConflictException exception)
        {
            logger.CriterionWeightUpdateFailed(exception);
            PublishedProfileScoresRecalculated = false;
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadRankingRangeAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.CriterionWeightUpdateFailed(exception);
            PublishedProfileScoresRecalculated = false;
            ModelState.AddModelError(string.Empty, "We could not save your changes. Try again later.");
        }

        await LoadPrioritisationDataAsync(cancellationToken);
        return Page();
    }

    private void ReadPendingChanges()
    {
        if (string.IsNullOrWhiteSpace(PendingChanges))
        {
            PendingCriteria = [];
            return;
        }

        try
        {
            PendingCriteria = JsonSerializer.Deserialize<List<PendingCriterionChange>>(PendingChanges, PendingChangesJson) ?? [];
        }
        catch (JsonException)
        {
            PendingCriteria = [];
            ModelState.AddModelError(nameof(PendingChanges), "Your pending criterion changes could not be read. Make them again and click Update.");
        }
    }

    /// <summary>Downloads the score data of the selected category as CSV - the legacy "Export score data" button.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The CSV file, or the page with an error when it could not be built.</returns>
    public async Task<IActionResult> OnGetExportScoreDataAsync(CancellationToken cancellationToken)
    {
        if (SelectedCategoryId is not { } categoryId)
        {
            return RedirectToPage();
        }

        try
        {
            var export = await prioritisationVariablesApiService.ExportCategoryScoreDataAsync(categoryId, cancellationToken);

            return export is null ? NotFound() : File(export.Content, CsvContentType, export.FileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.ScoreExportFailed(exception);
        }

        return await ShowExportErrorAsync("We could not export the score data. Try again later.", cancellationToken);
    }

    /// <summary>Downloads the normalised scores of every category as CSV - the legacy "Export normalised scores (all categories)" button.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The CSV file, or the page with an error when it could not be built.</returns>
    public async Task<IActionResult> OnGetExportNormalisedScoresAsync(CancellationToken cancellationToken)
    {
        try
        {
            var export = await prioritisationVariablesApiService.ExportNormalisedScoresAsync(cancellationToken);

            return File(export.Content, CsvContentType, export.FileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.ScoreExportFailed(exception);
        }

        return await ShowExportErrorAsync("We could not export the normalised scores. Try again later.", cancellationToken);
    }

    private async Task<PageResult> ShowExportErrorAsync(string message, CancellationToken cancellationToken)
    {
        await OnGetAsync(cancellationToken);
        ModelState.AddModelError(ExportErrorKey, message);
        return Page();
    }

    /// <summary>
    /// Validates the ranking range bounds and every pending criterion change, using the legacy
    /// messages prefixed with the criterion code, since the criterion panel is closed by then.
    /// </summary>
    private void ValidateRangeAndPendingCriteria()
    {
        if (LowerBound >= UpperBound)
        {
            ModelState.AddModelError(
                nameof(LowerBound),
                "Lower bound must be a positive integer that is lower than upper bound");
            ModelState.AddModelError(
                nameof(UpperBound),
                "Upper bound must be a positive integer that is greater than lower bound");
        }

        var criteria = Categories.SelectMany(category => category.Criteria).ToDictionary(criterion => criterion.Id);

        for (var i = 0; i < PendingCriteria.Count; i++)
        {
            var change = PendingCriteria[i];
            var key = $"{nameof(PendingCriteria)}[{i}]";

            if (!criteria.TryGetValue(change.Id, out var criterion))
            {
                ModelState.AddModelError(key, "A criterion with pending changes no longer exists");
                continue;
            }

            // The legacy editor stripped paragraphs and line breaks and limited the text, without formatting, to 255 characters.
            change.Name = SpeciesNameHtml.Clean(change.Name);
            var plainName = ProfileTitleHtmlFormatter.ToPlainText(change.Name);

            if (plainName.Length == 0)
            {
                ModelState.AddModelError(key, $"{criterion.Code}: You must enter criterion text");
            }
            else if (plainName.Length > MaxCriterionNameLength)
            {
                ModelState.AddModelError(key, $"{criterion.Code}: The criterion text must be no longer than 255 characters");
            }

            if (change.Weight is < 1 or > 999)
            {
                ModelState.AddModelError(key, $"{criterion.Code}: Criterion weight must be a positive integer");
            }

            foreach (var valueScore in change.ValueScores)
            {
                var value = criterion.Values.FirstOrDefault(candidate => candidate.Id == valueScore.Id);

                if (value is null)
                {
                    ModelState.AddModelError(key, $"{criterion.Code}: A value with pending changes no longer exists");
                }
                else if (valueScore.Score is < 0 or > 999)
                {
                    ModelState.AddModelError(key, $"{criterion.Code}: {value.Value} score must be a positive integer");
                }
            }
        }
    }

    private async Task LoadRankingRangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rankingRange = await prioritisationVariablesApiService.GetRankingRangeAsync(cancellationToken);
            LowerBound = rankingRange.LowerBound;
            UpperBound = rankingRange.UpperBound;
            RowVersion = rankingRange.RowVersion;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.PrioritisationCategoriesLoadFailed(exception);
        }
    }

    private async Task LoadPrioritisationDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            Categories = await prioritisationVariablesApiService.GetCategoriesAsync(cancellationToken);
            CategoriesErrorMessage = null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure or a non-success status code.
            // TaskCanceledException: the resilience handler's own request timeout expired.
            // NotSupportedException: the response was not valid JSON for PrioritisationCategoryDto[].
            logger.PrioritisationCategoriesLoadFailed(exception);

            Categories = [];
            CategoriesErrorMessage = "We could not load prioritisation categories. Try again later.";
        }

        SelectedCategoryCriteria = SelectedCategoryId is { } categoryId
            ? Categories.FirstOrDefault(category => category.Id == categoryId)?.Criteria ?? []
            : [];

        SelectedCriterion = SelectedCriterionId is { } criterionId
            ? SelectedCategoryCriteria.FirstOrDefault(criterion => criterion.Id == criterionId)
            : null;
    }
}

/// <summary>Source-generated structured log messages for this page.</summary>
internal static partial class PrioritisationVariablesLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load prioritisation categories")]
    public static partial void PrioritisationCategoriesLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save a criterion weighting change")]
    public static partial void CriterionWeightUpdateFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to prioritise draft profiles")]
    public static partial void DraftPrioritisationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to export prioritisation scores")]
    public static partial void ScoreExportFailed(this ILogger logger, Exception exception);
}
