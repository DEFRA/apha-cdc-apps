using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

public class HelpUsingD2R2Model : BreadcrumbPageModelBase
{
    private static readonly List<HelpDocumentRow> DocumentStore =
    [
        new HelpDocumentRow(
            "guidance",
            "Help using D2R2 guidance",
            "1.0",
            "22 September 2026",
            "/files/help-using-d2r2-guidance.pdf")
    ];

    public HelpUsingD2R2Model() : base("Help using D2R2")
    {
        Documents = DocumentStore;
    }

    public List<HelpDocumentRow> Documents { get; }

    public bool CanViewHistory { get; set; } = true;

    public bool CanDeleteDocuments { get; set; } = true;

    public void OnGet()
    {
    }

    public IActionResult OnPostDelete(string? documentId)
    {
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            DocumentStore.RemoveAll(x => x.DocumentId == documentId);
        }

        return RedirectToPage();
    }
}

public sealed record HelpDocumentRow(
    string DocumentId,
    string Title,
    string Version,
    string EffectiveDate,
    string DownloadUrl);
