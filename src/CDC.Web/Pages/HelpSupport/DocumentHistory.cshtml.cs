namespace CDC.Web.Pages.HelpSupport;

public class DocumentHistoryModel : BreadcrumbPageModelBase
{
    public DocumentHistoryModel() : base("Help using D2R2")
    {
        Versions =
        [
            new DocumentHistoryRow("Help using D2R2 guidance", "1.0", "22 September 2026"),
            new DocumentHistoryRow("Help using D2R2 guidance", "0.9", "15 August 2026")
        ];
    }

    public IReadOnlyList<DocumentHistoryRow> Versions { get; }

    public void OnGet(string? documentId)
    {
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            ViewData["DocumentId"] = documentId;
        }
    }
}

public sealed record DocumentHistoryRow(string Title, string Version, string EffectiveDate);