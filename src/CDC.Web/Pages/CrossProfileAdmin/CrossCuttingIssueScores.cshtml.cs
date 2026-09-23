using System.ComponentModel.DataAnnotations;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>
/// Lets Cross Profile Admin users select a cross-cutting issue, amend its score, and trigger a
/// recalculation of every cross-cutting issue's weighted score.
/// </summary>
public class CrossCuttingIssueScoresModel(ICrossCuttingIssueScoreService issueScoreService, ILogger<CrossCuttingIssueScoresModel> logger)
    : BreadcrumbPageModelBase("Maintain cross-cutting issue scores")
{
    /// <summary>Id of the issue chosen in the dropdown, carried on the query string between requests.</summary>
    [BindProperty(SupportsGet = true)]
    public int? SelectedIssueId { get; set; }

    [BindProperty]
    public ScoreInputModel Input { get; set; } = new();

    /// <summary>Options for the "cross-cutting issue" dropdown.</summary>
    public IReadOnlyList<SelectListItem> IssueOptions { get; private set; } = [];

    /// <summary>Every issue and its current weighted score, shown in the results table.</summary>
    public IReadOnlyList<CrossCuttingIssueScoreDto> AllIssues { get; private set; } = [];

    /// <summary>The issue selected in the dropdown, if any.</summary>
    public CrossCuttingIssueScoreDto? SelectedIssue { get; private set; }

    /// <summary>The current combined score across all cross-cutting issues.</summary>
    public decimal OverallScore { get; private set; }

    /// <summary>Whether to show the "scores updated" success banner.</summary>
    public bool ShowUpdatedBanner { get; private set; }

    public async Task OnGetAsync(bool updated, CancellationToken cancellationToken)
    {
        AllIssues = await issueScoreService.GetAllAsync(cancellationToken);
        IssueOptions = BuildOptions(AllIssues, SelectedIssueId);
        OverallScore = CalculateOverallScore(AllIssues);

        if (SelectedIssueId is int id)
        {
            SelectedIssue = AllIssues.FirstOrDefault(issue => issue.Id == id);
            if (SelectedIssue is not null)
            {
                Input.IssueId = SelectedIssue.Id;
                Input.Score = SelectedIssue.Score;
            }
        }

        ShowUpdatedBanner = updated && SelectedIssue is not null;
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        AllIssues = await issueScoreService.GetAllAsync(cancellationToken);
        SelectedIssueId = Input.IssueId;
        IssueOptions = BuildOptions(AllIssues, SelectedIssueId);
        OverallScore = CalculateOverallScore(AllIssues);
        SelectedIssue = AllIssues.FirstOrDefault(issue => issue.Id == Input.IssueId);

        if (SelectedIssue is null)
        {
            ModelState.AddModelError(nameof(Input.IssueId), "Select a cross-cutting issue.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await issueScoreService.UpdateScoreAsync(Input.IssueId, Input.Score!.Value, cancellationToken);
        logger.CrossCuttingIssueScoreUpdated(Input.IssueId, result.OverallScore);

        return RedirectToPage(new { SelectedIssueId = Input.IssueId, updated = true });
    }

    private static decimal CalculateOverallScore(IReadOnlyList<CrossCuttingIssueScoreDto> issues) =>
        issues.Count == 0 ? 0m : Math.Round(issues.Average(issue => issue.WeightedScore), 1);

    private static List<SelectListItem> BuildOptions(IReadOnlyList<CrossCuttingIssueScoreDto> issues, int? selectedId) =>
    [
        new SelectListItem("Select a cross-cutting issue", "", selectedId is null),
        .. issues.Select(issue => new SelectListItem(issue.Name, issue.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), issue.Id == selectedId))
    ];

    /// <summary>Bound values from the "amend score" form.</summary>
    public class ScoreInputModel
    {
        [Required(ErrorMessage = "Select a cross-cutting issue.")]
        public int IssueId { get; set; }

        [Required(ErrorMessage = "Enter a score.")]
        [Range(1, 5, ErrorMessage = "Enter a score between 1 and 5.")]
        public int? Score { get; set; }
    }
}

/// <summary>Source-generated structured log messages for <see cref="CrossCuttingIssueScoresModel"/>.</summary>
internal static partial class CrossCuttingIssueScoresLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information,
        Message = "Cross-cutting issue {IssueId} score updated; overall score recalculated to {OverallScore}")]
    public static partial void CrossCuttingIssueScoreUpdated(this ILogger logger, int issueId, decimal overallScore);
}
