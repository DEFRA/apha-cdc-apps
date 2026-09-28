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
        // Page renders static content only; no data to load.
    }
}
