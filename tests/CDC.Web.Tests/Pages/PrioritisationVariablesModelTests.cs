using CDC.Web.Pages.CrossProfileAdmin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages;

public class PrioritisationVariablesModelTests
{
    [Fact]
    public void OnPostRunDraftPrioritisation_SetsSuccessMessage()
    {
        var model = new PrioritisationVariablesModel();

        var result = model.OnPostRunDraftPrioritisation();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Prioritisation of draft profiles was successful", model.SuccessMessage);
    }

    [Fact]
    public void OnPostUpdateGlobalVariables_UpdatesPublishedScoreRecalculationState()
    {
        var model = new PrioritisationVariablesModel();

        var result = model.OnPostUpdateGlobalVariables();

        Assert.IsType<PageResult>(result);
        Assert.True(model.PublishedProfileScoresRecalculated);
        Assert.Equal("Published profile scores were recalculated successfully.", model.UpdateResultMessage);
    }

    [Fact]
    public void OnPostSaveRange_AppliesRevisedRangeToCategorisation()
    {
        var model = new PrioritisationVariablesModel
        {
            LowerBound = 15m,
            UpperBound = 35m
        };

        var result = model.OnPostSaveRange();

        Assert.IsType<PageResult>(result);
        Assert.Equal(15m, model.LowerBound);
        Assert.Equal(35m, model.UpperBound);
        Assert.Contains("15", model.CategorisationSummary);
        Assert.Contains("35", model.CategorisationSummary);
    }
}
