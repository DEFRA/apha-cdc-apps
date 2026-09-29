using System.Text.Json;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Maintains cross-cutting issue scores. Edits to a criterion's values are held as pending
/// changes in session - accumulating across criteria and categories - until Update commits them
/// all and triggers a recalculation, matching the legacy MaintainCrossCuttingIssueScores page.
/// </summary>
public class CrossCuttingIssueScoresModel(ICrossCuttingIssueScoreService issueScoreService, ILogger<CrossCuttingIssueScoresModel> logger)
    : BreadcrumbPageModelBase("Maintain cross-cutting issue scores")
{
    private const string PendingScoresSessionKey = "CrossCuttingIssuePendingScores";

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CriterionId { get; set; }

    /// <summary>
    /// Scores posted from the open criterion panel, keyed by criterion-value id. Nullable because
    /// only the Apply handler posts it. The explicit Name suppresses the binder's empty-prefix
    /// fallback, which would otherwise try to parse every posted form key as a Guid.
    /// </summary>
    [BindProperty(Name = "Scores")]
    public Dictionary<Guid, string?>? Scores { get; set; }

    public IReadOnlyList<CrossCuttingIssueCategory> Categories { get; private set; } = [];

    public IReadOnlyList<SelectListItem> CategoryOptions { get; private set; } = [];

    public CrossCuttingIssueCategory? SelectedCategory { get; private set; }

    /// <summary>The criterion whose values panel is open, if any.</summary>
    public CrossCuttingIssueCriterion? SelectedCriterion { get; private set; }

    /// <summary>Criterion values for the open panel, with any pending score applied.</summary>
    public IReadOnlyList<CrossCuttingIssueCriterionValue> SelectedCriterionValues { get; private set; } = [];

    public bool HasPendingChanges { get; private set; }

    /// <summary>Status text shown beside the Update button.</summary>
    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    /// <summary>Applies the open criterion's scores to the pending set without saving.</summary>
    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        if (SelectedCriterion is null)
        {
            return RedirectToPage(new { CategoryId });
        }

        var pending = ReadPending();
        var posted = Scores ?? [];

        foreach (var value in SelectedCriterion.Values)
        {
            var raw = posted.GetValueOrDefault(value.Id);

            if (string.IsNullOrWhiteSpace(raw))
            {
                ModelState.AddModelError($"Scores[{value.Id}]", $"Please enter a score for {value.Value}");
                continue;
            }

            if (!int.TryParse(raw, out var score) || score is < 0 or > 100)
            {
                ModelState.AddModelError($"Scores[{value.Id}]", "Score must be an integer between 0 and 100");
                continue;
            }

            if (score == value.Score)
            {
                pending.Remove(value.Id);
            }
            else
            {
                pending[value.Id] = score;
            }
        }

        if (!ModelState.IsValid)
        {
            // Keep the panel open showing what the user typed so errors can be corrected in place.
            SelectedCriterionValues =
            [
                .. SelectedCriterion.Values.Select(value =>
                    int.TryParse(posted.GetValueOrDefault(value.Id), out var typed)
                        ? value with { Score = typed }
                        : value)
            ];
            return Page();
        }

        WritePending(pending);

        return RedirectToPage(new { CategoryId });
    }

    /// <summary>Commits every pending score and triggers recalculation.</summary>
    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        var pending = ReadPending();

        if (pending.Count == 0)
        {
            await LoadAsync(cancellationToken);
            StatusMessage = "There are no changes to save";
            return Page();
        }

        await issueScoreService.SaveAsync(pending, cancellationToken);
        logger.CrossCuttingIssueScoresSaved(pending.Count);

        HttpContext.Session.Remove(PendingScoresSessionKey);

        await LoadAsync(cancellationToken);
        StatusMessage = "Your changes were successfully saved";
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Categories = await issueScoreService.GetMetadataAsync(cancellationToken);

        var pending = ReadPending();
        HasPendingChanges = pending.Count > 0;

        CategoryOptions =
        [
            new SelectListItem("Please select...", "", CategoryId is null),
            .. Categories.Select(category =>
                new SelectListItem(category.Name, category.Id.ToString(), category.Id == CategoryId))
        ];

        SelectedCategory = Categories.FirstOrDefault(category => category.Id == CategoryId);

        if (SelectedCategory is null)
        {
            return;
        }

        // Legacy auto-expands when a category has a single criterion.
        var criterionId = CriterionId ?? (SelectedCategory.Criteria.Count == 1 ? SelectedCategory.Criteria[0].Id : null);

        SelectedCriterion = SelectedCategory.Criteria.FirstOrDefault(criterion => criterion.Id == criterionId);

        if (SelectedCriterion is not null)
        {
            SelectedCriterionValues =
            [
                .. SelectedCriterion.Values.Select(value =>
                    pending.TryGetValue(value.Id, out var score) ? value with { Score = score } : value)
            ];
        }
    }

    private Dictionary<Guid, int> ReadPending()
    {
        var json = HttpContext.Session.GetString(PendingScoresSessionKey);

        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<Guid, int>>(json) ?? [];
    }

    private void WritePending(Dictionary<Guid, int> pending)
    {
        if (pending.Count == 0)
        {
            HttpContext.Session.Remove(PendingScoresSessionKey);
            return;
        }

        HttpContext.Session.SetString(PendingScoresSessionKey, JsonSerializer.Serialize(pending));
    }
}

/// <summary>Source-generated structured log messages for <see cref="CrossCuttingIssueScoresModel"/>.</summary>
internal static partial class CrossCuttingIssueScoresLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information,
        Message = "Committed {ChangedValueCount} cross-cutting issue criterion value score(s); recalculation triggered")]
    public static partial void CrossCuttingIssueScoresSaved(this ILogger logger, int changedValueCount);
}
