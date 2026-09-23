using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

public class QualityStatementModel : BreadcrumbPageModelBase
{
    private readonly IWebHostEnvironment _environment;

    public QualityStatementModel(IWebHostEnvironment environment) : base("D2R2 Quality Statement")
    {
        _environment = environment;
    }

    public IActionResult OnGet(bool download = true)
    {
        var pdfPath = Path.Combine(_environment.WebRootPath, "PDF", "DataQualityStatement_D2R2.pdf");

        if (!System.IO.File.Exists(pdfPath))
        {
            return NotFound();
        }

        var fileName = "D2R2-Quality-Statement.pdf";

        return PhysicalFile(pdfPath, "application/pdf", fileName);
    }
}
