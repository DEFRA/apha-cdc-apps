using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.CrossProfileAdmin;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class PrioritisationVariablesModelTests
{
    private static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task OnPostRunDraftPrioritisation_SetsSuccessMessage()
    {
        var model = CreatePageModel();

        var result = await model.OnPostRunDraftPrioritisation(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Prioritisation of draft profiles was successful", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostRunDraftPrioritisation_ClearsSpuriousRangeValidationErrors()
    {
        // The "Go" form doesn't submit LowerBound/UpperBound, so Razor Pages model binding
        // raises their [Required] errors on every post to this handler; they must not surface.
        var model = CreatePageModel();
        model.ModelState.AddModelError(nameof(PrioritisationVariablesModel.LowerBound), "Lower bound cannot be blank");
        model.ModelState.AddModelError(nameof(PrioritisationVariablesModel.UpperBound), "Upper bound cannot be blank");

        var result = await model.OnPostRunDraftPrioritisation(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.IsValid);
        Assert.Equal("Prioritisation of draft profiles was successful", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdate_SavesRangeAndTriggersRecalculation_WhenNoCriterionSelected()
    {
        var model = CreatePageModel();
        model.LowerBound = 15;
        model.UpperBound = 35;

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.PublishedProfileScoresRecalculated);
        Assert.Equal("Your changes were successfully saved", model.UpdateResultMessage);
        Assert.Contains("15", model.CategorisationSummary);
        Assert.Contains("35", model.CategorisationSummary);
    }

    [Fact]
    public async Task OnPostUpdate_RejectsLowerBoundNotLessThanUpperBound()
    {
        var model = CreatePageModel();
        model.LowerBound = 50;
        model.UpperBound = 50;

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.PublishedProfileScoresRecalculated);
        Assert.Null(model.UpdateResultMessage);
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnGetAsync_LoadsCategories()
    {
        var model = CreatePageModel();

        await model.OnGetAsync(CancellationToken.None);

        var category = Assert.Single(model.Categories);
        Assert.Equal("Animal welfare", category.Name);
    }

    [Fact]
    public async Task OnGetAsync_LoadsTheCurrentRankingRangeAndRowVersion()
    {
        var fakeService = new FakePrioritisationVariablesApiService(
            CreateCategories(),
            rankingRange: new PrioritisationRankingRangeDto { LowerBound = 20, UpperBound = 80, RowVersion = "AQIDBAUGBwg=" });
        var model = CreatePageModel(fakeService);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(20, model.LowerBound);
        Assert.Equal(80, model.UpperBound);
        Assert.Equal("AQIDBAUGBwg=", model.RowVersion);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesCriteria_ForTheSelectedCategory()
    {
        var model = CreatePageModel();
        model.SelectedCategoryId = CategoryId;

        await model.OnGetAsync(CancellationToken.None);

        var criterion = Assert.Single(model.SelectedCategoryCriteria);
        Assert.Equal("C1", criterion.Code);
        Assert.Equal(10, criterion.Weight);
    }

    [Fact]
    public async Task OnGetAsync_LeavesCriteriaEmpty_WhenNoCategorySelected()
    {
        var model = CreatePageModel();

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.SelectedCategoryCriteria);
    }

    [Fact]
    public async Task OnGetAsync_PrePopulatesCriterionWeightAndValueScores_ForTheSelectedCriterion()
    {
        var model = CreatePageModel();
        model.SelectedCategoryId = CategoryId;
        model.SelectedCriterionId = CriterionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(CriterionId, model.SelectedCriterion?.Id);
        Assert.Equal(10, model.CriterionWeight);
        var valueScore = Assert.Single(model.CriterionValueScores);
        Assert.Equal(ValueId, valueScore.Id);
        Assert.Equal(5, valueScore.Score);
    }

    [Fact]
    public async Task OnPostUpdate_SavesCriterionWeightAndValueScores_WhenCriterionSelected()
    {
        var fakeService = new FakePrioritisationVariablesApiService(CreateCategories());
        var model = CreatePageModel(fakeService);
        model.SelectedCategoryId = CategoryId;
        model.SelectedCriterionId = CriterionId;
        model.CriterionWeight = 42;
        model.CriterionValueScores = [new CriterionValueScoreInput { Id = ValueId, Score = 77 }];

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.IsValid);
        Assert.True(model.PublishedProfileScoresRecalculated);
        Assert.Equal("Your changes were successfully saved", model.UpdateResultMessage);
        Assert.Equal(CriterionId, fakeService.LastUpdatedCriterionId);
        Assert.Equal(42, fakeService.LastUpdatedWeight);
        var savedValueScore = Assert.Single(fakeService.LastUpdatedValueScores!);
        Assert.Equal(ValueId, savedValueScore.ValueId);
        Assert.Equal(77, savedValueScore.Score);
    }

    [Fact]
    public async Task OnPostUpdate_RejectsCriterionWeightOutOfRange()
    {
        var model = CreatePageModel();
        model.SelectedCategoryId = CategoryId;
        model.SelectedCriterionId = CriterionId;
        model.CriterionWeight = 0;
        model.CriterionValueScores = [new CriterionValueScoreInput { Id = ValueId, Score = 5 }];

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.False(model.PublishedProfileScoresRecalculated);
        Assert.Null(model.UpdateResultMessage);
    }

    [Fact]
    public async Task OnPostUpdate_RejectsValueScoreOutOfRange()
    {
        var model = CreatePageModel();
        model.SelectedCategoryId = CategoryId;
        model.SelectedCriterionId = CriterionId;
        model.CriterionWeight = 42;
        model.CriterionValueScores = [new CriterionValueScoreInput { Id = ValueId, Score = 1000 }];

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.False(model.PublishedProfileScoresRecalculated);
    }

    [Fact]
    public async Task OnPostUpdate_HandlesApiFailureGracefully()
    {
        var fakeService = new FakePrioritisationVariablesApiService(
            CreateCategories(),
            throwOnUpdateCriterion: new HttpRequestException("connection refused"));
        var model = CreatePageModel(fakeService);
        model.SelectedCategoryId = CategoryId;
        model.SelectedCriterionId = CriterionId;
        model.CriterionWeight = 42;
        model.CriterionValueScores = [new CriterionValueScoreInput { Id = ValueId, Score = 5 }];

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.False(model.PublishedProfileScoresRecalculated);
        Assert.Null(model.UpdateResultMessage);
    }

    [Fact]
    public async Task OnPostUpdate_SavesRangeViaTheApi()
    {
        var fakeService = new FakePrioritisationVariablesApiService(CreateCategories());
        var model = CreatePageModel(fakeService);
        model.LowerBound = 15;
        model.UpperBound = 35;
        model.RowVersion = "AQIDBAUGBwg=";

        await model.OnPostUpdate(CancellationToken.None);

        Assert.NotNull(fakeService.LastSavedRankingRange);
        Assert.Equal(15, fakeService.LastSavedRankingRange!.LowerBound);
        Assert.Equal(35, fakeService.LastSavedRankingRange!.UpperBound);
        Assert.Equal("AQIDBAUGBwg=", fakeService.LastSavedRankingRange!.RowVersion);
    }

    [Fact]
    public async Task OnPostUpdate_ReportsConflict_WhenAnotherUserSavedFirst()
    {
        var fakeService = new FakePrioritisationVariablesApiService(
            CreateCategories(),
            rankingRange: new PrioritisationRankingRangeDto { LowerBound = 5, UpperBound = 95, RowVersion = "CQoLDA0ODxA=" },
            throwOnUpdateRankingRange: new RankingRangeConflictException(
                "Someone else has updated the prioritisation variables. Reload the page and try again."));
        var model = CreatePageModel(fakeService);
        model.LowerBound = 15;
        model.UpperBound = 35;
        model.RowVersion = "AQIDBAUGBwg=";

        var result = await model.OnPostUpdate(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.False(model.PublishedProfileScoresRecalculated);
        Assert.Null(model.UpdateResultMessage);
        Assert.Contains(
            model.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage.Contains("Reload the page", StringComparison.Ordinal));

        // The current stored range is reloaded so the user can review it before retrying.
        Assert.Equal(5, model.LowerBound);
        Assert.Equal(95, model.UpperBound);
        Assert.Equal("CQoLDA0ODxA=", model.RowVersion);
    }

    private static IReadOnlyList<PrioritisationCategoryDto> CreateCategories() =>
    [
        new PrioritisationCategoryDto
        {
            Id = CategoryId,
            Name = "Animal welfare",
            Criteria =
            [
                new PrioritisationCriterionDto
                {
                    Id = CriterionId,
                    Code = "C1",
                    Name = "Impact",
                    Weight = 10,
                    Values = [new PrioritisationCriterionValueDto { Id = ValueId, Value = "N/A", Score = 5 }]
                }
            ]
        }
    ];

    private static PrioritisationVariablesModel CreatePageModel() =>
        CreatePageModel(new FakePrioritisationVariablesApiService(CreateCategories()));

    private static PrioritisationVariablesModel CreatePageModel(FakePrioritisationVariablesApiService fakeService) =>
        new(fakeService, NullLogger<PrioritisationVariablesModel>.Instance);
}

