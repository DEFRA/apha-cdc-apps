using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// "Help using D2R2": the library of user manuals a Profile Editor maintains and every user can
/// browse and download. Replaces the legacy <c>StaticReports.aspx?UserManual=1</c> page.
/// </summary>
/// <remarks>
/// Real authentication (Entra ID) is not wired up yet, so <see cref="CanViewHistory"/> and
/// <see cref="CanManageDocuments"/> are placeholders until requests carry an authenticated
/// caller's role - see the equivalent note on <c>SpeciesAuditOptions</c>. The legacy
/// <c>StaticReportList.CanGetList</c>/<c>StaticReport.CanDelete</c> rules restrict history to
/// authenticated users and add/delete to the Profile Editor role.
/// </remarks>
/// <param name="staticReportsApiService">Typed client for the static reports endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class HelpUsingD2R2Model(IStaticReportsApiService staticReportsApiService, ILogger<HelpUsingD2R2Model> logger)
    : BreadcrumbPageModelBase("Help using D2R2")
{
    private const string PdfContentType = "application/pdf";
    public const int DefaultPageSize = 10;

    public static IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 30, 50];

    /// <summary>Gets the current version of every user manual.</summary>
    public IReadOnlyList<StaticReportVersionDto> Documents { get; private set; } = [];

    /// <summary>Gets or sets the current page number in the browser query string.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets how many documents appear on a page.</summary>
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Gets the subset of documents shown on the current page.</summary>
    public IReadOnlyList<StaticReportVersionDto> PagedDocuments =>
        Documents.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();

    /// <summary>Gets the total number of pages in the document list.</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)Documents.Count / Math.Max(PageSize, 1)));

    /// <summary>Gets a value indicating whether the history link is shown.</summary>
    public bool CanViewHistory { get; } = true;

    /// <summary>Gets a value indicating whether the add and delete controls are shown.</summary>
    public bool CanManageDocuments { get; } = true;

    /// <summary>Gets the outcome message to display after a load, upload or delete, if any.</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="StatusMessage"/> describes a failure.</summary>
    public bool StatusIsError { get; private set; }

    /// <summary>Gets a value indicating whether the upload section should be rendered open, so a
    /// failed upload leaves the user on it to choose another file.</summary>
    public bool ShowUploadPanel { get; private set; }

    public async Task OnGetAsync(int? pageNumber = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        await LoadDocumentsAsync(cancellationToken);
        ApplyPaging(pageNumber, pageSize);
    }

    /// <summary>Streams a manual's PDF content back to the browser.</summary>
    /// <param name="documentId">The static report version to download.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF content, or a 404 when the version no longer exists.</returns>
    public async Task<IActionResult> OnGetDownloadAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var data = await staticReportsApiService.GetDataAsync(documentId, cancellationToken);

            return data is null
                ? NotFound()
                : File(data.PdfData, PdfContentType);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.StaticReportDownloadFailed(exception, documentId);

            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Uploads a new manual version.</summary>
    /// <param name="document">The Pdf file browsed and selected by the user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IActionResult> OnPostUploadAsync(IFormFile? document, CancellationToken cancellationToken)
    {
        if (!CanManageDocuments)
        {
            return Forbid();
        }

        const string failurePrefix = "Failed to upload the user manual: ";

        if (document is null || document.Length == 0)
        {
            StatusMessage = failurePrefix + "Choose a file to upload.";
            StatusIsError = true;
        }
        else if (!string.Equals(document.ContentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = failurePrefix + "The uploaded user manual must be a PDF.";
            StatusIsError = true;
        }
        else
        {
            await using var stream = new MemoryStream();
            await document.CopyToAsync(stream, cancellationToken);

            var title = Path.GetFileNameWithoutExtension(document.FileName);
            var request = new UploadStaticReportRequestDto
            {
                Title = title,
                PdfData = stream.ToArray(),
                IsUserManual = true,
                IsPublic = false
            };

            try
            {
                var result = await staticReportsApiService.UploadAsync(request, cancellationToken);

                StatusIsError = result.Outcome != StaticReportUpdateOutcome.Success;
                StatusMessage = StatusIsError
                    ? failurePrefix + result.ErrorMessage
                    : "The user manual was uploaded successfully.";
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                logger.StaticReportUploadFailed(exception, title);

                StatusIsError = true;
                StatusMessage = failurePrefix + "We could not upload this document. Try again later.";
            }
        }

        // Keep the upload section open on failure, so the user can choose another file without
        // re-opening it; a successful upload collapses it back since there's nothing left to do.
        ShowUploadPanel = StatusIsError;

        await LoadDocumentsAsync(cancellationToken);

        return Page();
    }

    /// <summary>Deletes a manual version.</summary>
    /// <param name="documentId">The static report version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IActionResult> OnPostDeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        if (!CanManageDocuments)
        {
            return Forbid();
        }

        try
        {
            var result = await staticReportsApiService.DeleteAsync(documentId, cancellationToken);

            StatusIsError = result.Outcome != StaticReportUpdateOutcome.Success;
            StatusMessage = StatusIsError
                ? result.ErrorMessage
                : "The user manual version was successfully deleted.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.StaticReportDeleteFailed(exception, documentId);

            StatusIsError = true;
            StatusMessage = "We could not delete this document. Try again later.";
        }

        await LoadDocumentsAsync(cancellationToken);

        return Page();
    }

    private void ApplyPaging(int? requestedPageNumber, int? requestedPageSize)
    {
        var validPageSize = requestedPageSize is > 0 && PageSizeOptions.Contains(requestedPageSize.Value)
            ? requestedPageSize.Value
            : DefaultPageSize;

        PageSize = validPageSize;
        PageNumber = Math.Clamp(requestedPageNumber ?? 1, 1, TotalPages);
    }

    public string BuildPageUrl(int targetPage)
    {
        var safePageNumber = Math.Clamp(targetPage, 1, TotalPages);
        return $"?pageNumber={safePageNumber}&pageSize={PageSize}";
    }

    private async Task LoadDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            Documents = await staticReportsApiService.GetCurrentAsync(isUserManual: true, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.StaticReportListLoadFailed(exception);

            Documents = [];
            StatusIsError = true;
            StatusMessage = "We could not load the list of user manuals. Try again later.";
        }
    }
}

/// <summary>Source-generated structured log messages for <see cref="HelpUsingD2R2Model"/>.</summary>
internal static partial class HelpUsingD2R2Log
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Error, Message = "Failed to load the list of user manuals from CDC.Api")]
    public static partial void StaticReportListLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Failed to download static report version {DocumentId} from CDC.Api")]
    public static partial void StaticReportDownloadFailed(this ILogger logger, Exception exception, Guid documentId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Failed to upload user manual '{Title}' to CDC.Api")]
    public static partial void StaticReportUploadFailed(this ILogger logger, Exception exception, string title);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Failed to delete static report version {DocumentId} via CDC.Api")]
    public static partial void StaticReportDeleteFailed(this ILogger logger, Exception exception, Guid documentId);
}
