using System.Globalization;
using System.Text.Json;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.CrossProfileAdmin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class CrossCuttingIssueScoresModelTests
{
    private static readonly Guid BioSecurityId = Guid.Parse("E0C3A33E-CFAC-4033-83C0-A88AD53417B7");
    private static readonly Guid LivestockContactsId = Guid.Parse("10F04FB1-FEFA-49B6-B1C0-BA204C12B1B4");
    private static readonly Guid RegularlyMixId = Guid.Parse("88AD7510-9D77-474A-B3C5-5FC32B98F380");
    private static readonly Guid MovementsId = Guid.Parse("5B1A65EA-A19A-4239-8A32-DCC06E518598");

    [Fact]
    public async Task OnGetAsync_OffersEveryCrossCuttingIssueForSelection()
    {
        var pageModel = CreatePageModel();

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(4, pageModel.Categories.Count);
        Assert.Equal(
            ["Animal identification", "Movements", "Animal locations and number", "Bio-security"],
            pageModel.Categories.Select(category => category.Name));
        Assert.Equal("Please select...", pageModel.CategoryOptions[0].Text);
        Assert.Null(pageModel.SelectedCategory);
    }

    [Fact]
    public async Task OnGetAsync_AutoExpandsTheOnlyCriterion()
    {
        var pageModel = CreatePageModel();
        pageModel.CategoryId = MovementsId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.NotNull(pageModel.SelectedCriterion);
        Assert.Equal("Ease of movement investigation", pageModel.SelectedCriterion!.Name);
    }

    [Fact]
    public async Task OnPostApplyAsync_HoldsAmendedScoreAsPendingWithoutSaving()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = CreatePageModel(service);
        pageModel.CategoryId = BioSecurityId;
        pageModel.CriterionId = LivestockContactsId;
        pageModel.Scores = ScoresFor(service, LivestockContactsId, RegularlyMixId, "75");

        var result = await pageModel.OnPostApplyAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(service.SaveCalled);
        Assert.Equal(75, ReadPending(pageModel)[RegularlyMixId]);
    }

    [Fact]
    public async Task PendingChanges_SurviveSwitchingToAnotherCategory()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = CreatePageModel(service);
        pageModel.CategoryId = BioSecurityId;
        pageModel.CriterionId = LivestockContactsId;
        pageModel.Scores = ScoresFor(service, LivestockContactsId, RegularlyMixId, "75");
        await pageModel.OnPostApplyAsync(CancellationToken.None);

        // Same session, different category selected.
        var next = CreatePageModel(service, pageModel.HttpContext.Session);
        next.CategoryId = MovementsId;
        await next.OnGetAsync(CancellationToken.None);

        Assert.True(next.HasPendingChanges);
        Assert.False(service.SaveCalled);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("abc")]
    public async Task OnPostApplyAsync_RejectsScoreOutsideZeroToOneHundred(string score)
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = CreatePageModel(service);
        pageModel.CategoryId = BioSecurityId;
        pageModel.CriterionId = LivestockContactsId;
        pageModel.Scores = ScoresFor(service, LivestockContactsId, RegularlyMixId, score);

        var result = await pageModel.OnPostApplyAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Empty(ReadPending(pageModel));
    }

    [Fact]
    public async Task OnPostUpdateAsync_CommitsPendingScoresAndTriggersRecalculation()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = CreatePageModel(service);
        pageModel.CategoryId = BioSecurityId;
        pageModel.CriterionId = LivestockContactsId;
        pageModel.Scores = ScoresFor(service, LivestockContactsId, RegularlyMixId, "80");
        await pageModel.OnPostApplyAsync(CancellationToken.None);

        var committed = CreatePageModel(service, pageModel.HttpContext.Session);
        await committed.OnPostUpdateAsync(CancellationToken.None);

        Assert.True(service.SaveCalled);
        Assert.Equal(80, service.SavedScores[RegularlyMixId]);
        Assert.Equal("Your changes were successfully saved", committed.StatusMessage);
        Assert.False(committed.HasPendingChanges);
    }

    [Fact]
    public async Task OnPostUpdateAsync_WithNoPendingChanges_DoesNotSave()
    {
        var service = new FakeCrossCuttingIssueScoreService();
        var pageModel = CreatePageModel(service);

        await pageModel.OnPostUpdateAsync(CancellationToken.None);

        Assert.False(service.SaveCalled);
        Assert.Equal("There are no changes to save", pageModel.StatusMessage);
    }

    [Fact]
    public async Task CancellingACriterion_LeavesScoresUnchangedAndTriggersNoRecalculation()
    {
        var service = new FakeCrossCuttingIssueScoreService();

        // Cancel is a plain link back to the category, so no Apply ever runs.
        var pageModel = CreatePageModel(service);
        pageModel.CategoryId = BioSecurityId;
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.HasPendingChanges);
        Assert.False(service.SaveCalled);
        Assert.Empty(ReadPending(pageModel));
    }

    [Fact]
    public async Task SeededScores_AreZeroToOneHundredScale()
    {
        var service = new FakeCrossCuttingIssueScoreService();

        var categories = await service.GetMetadataAsync();
        var allScores = categories.SelectMany(c => c.Criteria).SelectMany(c => c.Values).Select(v => v.Score).ToList();

        Assert.NotEmpty(allScores);
        Assert.All(allScores, score => Assert.InRange(score, 0, 100));
    }

    private static Dictionary<Guid, string?> ScoresFor(
        FakeCrossCuttingIssueScoreService service, Guid criterionId, Guid valueId, string score)
    {
        var criterion = service.GetMetadataAsync().Result
            .SelectMany(category => category.Criteria)
            .Single(item => item.Id == criterionId);

        // Every value in the open panel posts back, not just the amended one.
        return criterion.Values.ToDictionary(
            value => value.Id,
            value => value.Id == valueId ? score : value.Score.ToString(CultureInfo.InvariantCulture))!;
    }

    private static Dictionary<Guid, int> ReadPending(CrossCuttingIssueScoresModel pageModel)
    {
        var json = pageModel.HttpContext.Session.GetString("CrossCuttingIssuePendingScores");
        return string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<Dictionary<Guid, int>>(json)!;
    }

    private static CrossCuttingIssueScoresModel CreatePageModel(
        FakeCrossCuttingIssueScoreService? service = null, ISession? session = null)
    {
        var httpContext = new DefaultHttpContext { Session = session ?? new FakeSession() };

        return new CrossCuttingIssueScoresModel(
            service ?? new FakeCrossCuttingIssueScoreService(),
            NullLogger<CrossCuttingIssueScoresModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }

    private sealed class FakeCrossCuttingIssueScoreService : ICrossCuttingIssueScoreService
    {
        private readonly InMemoryCrossCuttingIssueScoreService _inner = new();

        public bool SaveCalled { get; private set; }

        public Dictionary<Guid, int> SavedScores { get; } = [];

        public Task<IReadOnlyList<CrossCuttingIssueCategory>> GetMetadataAsync(CancellationToken cancellationToken = default) =>
            _inner.GetMetadataAsync(cancellationToken);

        public Task SaveAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken = default)
        {
            SaveCalled = true;
            foreach (var (id, score) in scores)
            {
                SavedScores[id] = score;
            }

            return _inner.SaveAsync(scores, cancellationToken);
        }
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
