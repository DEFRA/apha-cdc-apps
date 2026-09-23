using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.CrossProfileAdmin;

public class PrioritisationVariablesModel : BreadcrumbPageModelBase
{
    public PrioritisationVariablesModel() : base("Maintain prioritisation variables")
    {
        LowerBound = 0m;
        UpperBound = 100m;
        CategorisationSummary = $"Current sourcing range is {LowerBound} to {UpperBound}.";
    }

    [BindProperty]
    public decimal LowerBound { get; set; }

    [BindProperty]
    public decimal UpperBound { get; set; }

    public string? SuccessMessage { get; private set; }

    public string? UpdateResultMessage { get; private set; }

    public bool PublishedProfileScoresRecalculated { get; private set; }

    public string CategorisationSummary { get; private set; }

    public void OnGet()
    {
        UpdateCategorisationSummary();
    }

    public PageResult OnPostRunDraftPrioritisation()
    {
        SuccessMessage = "Prioritisation of draft profiles was successful";
        UpdateResultMessage = null;
        PublishedProfileScoresRecalculated = false;
        return Page();
    }

    public PageResult OnPostUpdateGlobalVariables()
    {
        PublishedProfileScoresRecalculated = true;
        UpdateResultMessage = "Published profile scores were recalculated successfully.";
        SuccessMessage = null;
        return Page();
    }

    public PageResult OnPostSaveRange()
    {
        if (LowerBound > UpperBound)
        {
            ModelState.AddModelError(string.Empty, "Lower bound cannot be greater than upper bound.");
            return Page();
        }

        UpdateCategorisationSummary();
        return Page();
    }

    private void UpdateCategorisationSummary()
    {
        CategorisationSummary = $"Current categorisation range is {LowerBound} to {UpperBound}. Profiles within this band are grouped together for prioritisation reporting.";
    }
}
