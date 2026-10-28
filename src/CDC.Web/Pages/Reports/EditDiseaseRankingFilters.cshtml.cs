using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.Reports;

[Authorize(Policy = AuthorizationPolicies.CanManageDiseaseRankingFilters)]
public class EditDiseaseRankingFiltersModel : BreadcrumbPageModelBase
{
    public EditDiseaseRankingFiltersModel() : base("Edit disease ranking filters")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
