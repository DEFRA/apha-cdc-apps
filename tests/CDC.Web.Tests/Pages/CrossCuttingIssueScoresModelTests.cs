using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.CrossProfileAdmin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class CrossCuttingIssueScoresModelTests
{
    [Fact]
    public async Task OnGetAsync_LoadsIssuesAndPreselectsChosenIssue()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = new CrossCuttingIssueScoresModel(service, NullLogger<CrossCuttingIssueScoresModel>.Instance)
        {
            SelectedIssueId = 2
        };

        await pageModel.OnGetAsync(updated: true, CancellationToken.None);

        Assert.Equal(5, pageModel.AllIssues.Count);
        Assert.Equal(2, pageModel.SelectedIssueId);
        Assert.Equal("Public health significance", pageModel.SelectedIssue!.Name);
        Assert.Equal(4, pageModel.Input.Score);
        Assert.True(pageModel.ShowUpdatedBanner);
        Assert.Contains(pageModel.IssueOptions, option => option.Value == "2" && option.Selected);
    }

    [Fact]
    public async Task OnPostUpdateAsync_WithValidData_UpdatesScoreAndRedirects()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = new CrossCuttingIssueScoresModel(service, NullLogger<CrossCuttingIssueScoresModel>.Instance)
        {
            Input = new CrossCuttingIssueScoresModel.ScoreInputModel { IssueId = 3, Score = 5 }
        };

        var result = await pageModel.OnPostUpdateAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(3, redirect.RouteValues!["SelectedIssueId"]);
        Assert.True((bool)redirect.RouteValues["updated"]!);
        Assert.Equal(5, (await service.GetByIdAsync(3)).Score);
    }

    [Fact]
    public async Task OnPostUpdateAsync_WithInvalidSelection_ReturnsPageAndModelErrors()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = new CrossCuttingIssueScoresModel(service, NullLogger<CrossCuttingIssueScoresModel>.Instance)
        {
            Input = new CrossCuttingIssueScoresModel.ScoreInputModel { IssueId = 99, Score = 6 }
        };

        var result = await pageModel.OnPostUpdateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.ContainsKey(nameof(CrossCuttingIssueScoresModel.ScoreInputModel.IssueId)));
        Assert.Contains(pageModel.ModelState.Values.SelectMany(v => v.Errors), error => error.ErrorMessage.Contains("Select a cross-cutting issue"));
    }

    [Fact]
    public async Task CrossCuttingIssueService_RecalculatesScoresAndRejectsBadInput()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var allIssues = await service.GetAllAsync();

        Assert.Equal(5, allIssues.Count);
        Assert.Equal("Public health significance", allIssues.Single(issue => issue.Id == 2).Name);

        var recalculated = await service.UpdateScoreAsync(4, 5);
        Assert.Equal(5, recalculated.Issues.Single(issue => issue.Id == 4).Score);
        Assert.Equal(5m, recalculated.Issues.Single(issue => issue.Id == 4).WeightedScore);
        Assert.True(recalculated.OverallScore > 0m);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpdateScoreAsync(1, 0));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateScoreAsync(99, 3));
    }

    private sealed class FakeCrossCuttingIssueScoreService : ICrossCuttingIssueScoreService
    {
        private readonly Dictionary<int, (string Name, decimal Weight, int Score)> _issues = new()
        {
            [1] = ("Trade sensitivity", 1.2m, 3),
            [2] = ("Public health significance", 1.5m, 4),
            [3] = ("Zoonotic potential", 1.5m, 3),
            [4] = ("International reporting obligations", 1.0m, 2),
            [5] = ("Reputational risk", 0.8m, 2)
        };

        public Task<IReadOnlyList<CrossCuttingIssueScoreDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CrossCuttingIssueScoreDto>>(
                _issues.Select(pair => new CrossCuttingIssueScoreDto(pair.Key, pair.Value.Name, pair.Value.Score, Math.Round(pair.Value.Score * pair.Value.Weight, 1)))
                    .OrderBy(issue => issue.Name)
                    .ToList());
        }

        public Task<CrossCuttingIssueScoreDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var issue = _issues.GetValueOrDefault(id);
            if (issue == default) return Task.FromResult<CrossCuttingIssueScoreDto?>(null);
            return Task.FromResult<CrossCuttingIssueScoreDto?>(new CrossCuttingIssueScoreDto(id, issue.Name, issue.Score, Math.Round(issue.Score * issue.Weight, 1)));
        }

        public Task<CrossCuttingIssueRecalculationResultDto> UpdateScoreAsync(int id, int score, CancellationToken cancellationToken = default)
        {
            if (score is < 1 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }

            if (!_issues.TryGetValue(id, out var issue))
            {
                throw new KeyNotFoundException($"Cross-cutting issue {id} was not found.");
            }

            _issues[id] = (issue.Name, issue.Weight, score);
            var refreshed = _issues.Select(pair => new CrossCuttingIssueScoreDto(pair.Key, pair.Value.Name, pair.Value.Score, Math.Round(pair.Value.Score * pair.Value.Weight, 1)))
                .OrderBy(item => item.Name)
                .ToList();
            var overall = Math.Round(refreshed.Average(item => item.WeightedScore), 1);
            return Task.FromResult(new CrossCuttingIssueRecalculationResultDto(refreshed, overall, DateTimeOffset.UtcNow));
        }
    }
}
