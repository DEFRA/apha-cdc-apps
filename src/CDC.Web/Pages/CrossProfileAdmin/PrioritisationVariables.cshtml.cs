using System.ComponentModel.DataAnnotations;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
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

// Mirrors the legacy MaintainPrioritisationVariables.aspx/.vb page: a separate "Go" action for
// draft-profile prioritisation, a single "Update" action that validates and saves the ranking
// range plus (when a criterion is selected) its weighting and value scores together, and a
// category-scoped criteria list (the "search results" grid shown after selecting a category).
public class PrioritisationVariablesModel(
    IPrioritisationVariablesApiService prioritisationVariablesApiService,
    ILogger<PrioritisationVariablesModel> logger)
    : BreadcrumbPageModelBase("Maintain prioritisation variables")
{
    [BindProperty]
    [Required(ErrorMessage = "Lower bound cannot be blank")]
    [Range(0, 998, ErrorMessage = "Lower bound must be a positive integer")]
    public int LowerBound { get; set; } = 0;

    [BindProperty]
    [Required(ErrorMessage = "Upper bound cannot be blank")]
    [Range(1, 999, ErrorMessage = "Upper bound must be a positive integer")]
    public int UpperBound { get; set; } = 100;

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

    /// <summary>The weighting entered in the criterion edit panel. Not validated via attributes,
    /// so that posting to the "Go" handler doesn't raise spurious errors - see <see cref="OnPostUpdate"/>.</summary>
    [BindProperty]
    public int CriterionWeight { get; set; }

    /// <summary>The per-value scores entered in the criterion edit panel.</summary>
    [BindProperty]
    public List<CriterionValueScoreInput> CriterionValueScores { get; set; } = [];

    public string? SuccessMessage { get; private set; }

    public string? UpdateResultMessage { get; private set; }

    public bool PublishedProfileScoresRecalculated { get; private set; }

    public string CategorisationSummary { get; private set; } = string.Empty;

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
        CategorisationSummary = BuildCategorisationSummary();
        await LoadPrioritisationDataAsync(cancellationToken);

        if (SelectedCriterion is not null)
        {
            CriterionWeight = SelectedCriterion.Weight;
            CriterionValueScores = [.. SelectedCriterion.Values.Select(value => new CriterionValueScoreInput { Id = value.Id, Score = value.Score })];
        }
    }

    public async Task<PageResult> OnPostRunDraftPrioritisation(CancellationToken cancellationToken)
    {
        // The ranking range and Go are one form (matches legacy's single-page postback), so
        // LowerBound/UpperBound/RowVersion bind from whatever is currently on screen, saved or
        // not; range validation only applies to the Update handler, so clear it here.
        ModelState.Clear();

        SuccessMessage = "Prioritisation of draft profiles was successful";
        UpdateResultMessage = null;
        PublishedProfileScoresRecalculated = false;
        CategorisationSummary = BuildCategorisationSummary();
        await LoadPrioritisationDataAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// The single "Update" action: saves the ranking range, and - when a criterion is selected -
    /// its weighting and every value's score, together, exactly as the legacy single Update
    /// button did.
    /// </summary>
    public async Task<PageResult> OnPostUpdate(CancellationToken cancellationToken)
    {
        SuccessMessage = null;

        ValidateRangeAndCriterionInputs();

        if (!ModelState.IsValid)
        {
            UpdateResultMessage = null;
            PublishedProfileScoresRecalculated = false;
            CategorisationSummary = BuildCategorisationSummary();
            await LoadPrioritisationDataAsync(cancellationToken);
            return Page();
        }

        try
        {
            var updatedRange = await prioritisationVariablesApiService.UpdateRankingRangeAsync(
                new PrioritisationRankingRangeDto { LowerBound = LowerBound, UpperBound = UpperBound, RowVersion = RowVersion },
                cancellationToken);
            RowVersion = updatedRange.RowVersion;

            if (SelectedCriterionId is not null)
            {
                var valueScores = CriterionValueScores
                    .Select(value => new CriterionValueScore { ValueId = value.Id, Score = value.Score })
                    .ToList();

                await prioritisationVariablesApiService.UpdateCriterionAsync(SelectedCriterionId.Value, CriterionWeight, valueScores, cancellationToken);
            }

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

        CategorisationSummary = BuildCategorisationSummary();
        await LoadPrioritisationDataAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Validates the ranking range bounds and, when a criterion is selected, its weighting and
    /// every value's score - split out of <see cref="OnPostUpdate"/> to keep its complexity down.
    /// </summary>
    private void ValidateRangeAndCriterionInputs()
    {
        if (LowerBound >= UpperBound)
        {
            ModelState.AddModelError(
                nameof(LowerBound),
                "Lower bound must be a positive integer that is lower than upper bound");
        }

        if (SelectedCriterionId is null)
        {
            return;
        }

        if (CriterionWeight is < 1 or > 999)
        {
            ModelState.AddModelError(nameof(CriterionWeight), "Criterion weight must be a positive integer");
        }

        for (var i = 0; i < CriterionValueScores.Count; i++)
        {
            if (CriterionValueScores[i].Score is < 0 or > 999)
            {
                ModelState.AddModelError($"{nameof(CriterionValueScores)}[{i}].{nameof(CriterionValueScoreInput.Score)}", "Score must be a positive integer");
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

    private string BuildCategorisationSummary() =>
        $"Current categorisation range is {LowerBound} to {UpperBound}. Profiles within this band are grouped together for prioritisation reporting.";
}

/// <summary>Source-generated structured log messages for this page.</summary>
internal static partial class PrioritisationVariablesLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load prioritisation categories")]
    public static partial void PrioritisationCategoriesLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save a criterion weighting change")]
    public static partial void CriterionWeightUpdateFailed(this ILogger logger, Exception exception);
}
