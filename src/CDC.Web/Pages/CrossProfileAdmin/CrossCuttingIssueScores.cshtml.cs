using System.ComponentModel.DataAnnotations;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CDC.Web.Pages.CrossProfileAdmin;

public class CrossCuttingIssueScoresModel : BreadcrumbPageModelBase
{
    public CrossCuttingIssueScoresModel() : base("Maintain cross-cutting issue scores")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
