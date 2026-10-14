using CDC.Common.Contracts;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Lets a user tick sections, questions and guidance of a profile version and download them as a
/// bespoke report PDF, and lets a profile editor save, update, rename and delete the selections as
/// templates. Replaces the legacy <c>CreateBespokeReport.aspx</c>; the PDF is built with a Razor
/// view and Playwright instead of TallPDF.
/// </summary>
/// <param name="reportsApi">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="templatesApi">Typed client for the bespoke report template endpoints on CDC.Api.</param>
/// <param name="pdfBuilder">Builds the bespoke report PDF.</param>
/// <param name="navigationAuthorization">Evaluates the permissions that decide what the page shows.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanViewBespokeReport)]
public class CreateBespokeReportModel(
    IProfileReportsApiService reportsApi,
    IBespokeReportTemplatesApiService templatesApi,
    IBespokeReportPdfBuilder pdfBuilder,
    INavigationAuthorizationService navigationAuthorization,
    ILogger<CreateBespokeReportModel> logger) : PageModel
{
    /// <summary>The introduction shown to a profile editor, who can also save templates.</summary>
    public const string EditorIntroText =
        "Using the tree below, select sections, questions and guidance to create a bespoke report on the selected disease profile. To save, update or delete a template, select a template from the dropdown below. Template titles can be edited once a template has been selected from the dropdown.";

    /// <summary>The introduction shown to a user who may only generate reports from templates.</summary>
    public const string ViewerIntroText = "Using the dropdown below, select a template to generate a bespoke report on the selected disease profile.";

    /// <summary>The message shown when the page could not be loaded.</summary>
    public const string LoadFailureMessage = "We could not load the bespoke report page. Try again later.";

    /// <summary>The message shown when the bespoke report could not be built.</summary>
    public const string GenerateFailureMessage = "We could not generate the bespoke report. Try again later.";

    /// <summary>The message shown when a template change could not be saved.</summary>
    public const string SaveFailureMessage = "We could not save the template. Try again later.";

    /// <summary>The message shown when the selected template no longer exists.</summary>
    public const string TemplateMissingMessage = "The template no longer exists.";

    /// <summary>The message shown when "Generate bespoke report" is used with nothing ticked.</summary>
    public const string NothingSelectedMessage = "Select at least one section, question or guidance to include in the report.";

    /// <summary>The message shown after a template was saved, as the legacy page's script worded it.</summary>
    public const string TemplateSavedMessage = "Template saved";

    /// <summary>The message shown after a template's title was saved.</summary>
    public const string TemplateTitleSavedMessage = "Template title saved";

    /// <summary>The message shown after a template was deleted.</summary>
    public const string TemplateDeletedMessage = "Template deleted";

    /// <summary>The identifier of the "Save template as" dialog.</summary>
    public const string SaveAsDialog = "save-as-dialog";

    /// <summary>The identifier of the "Edit title" dialog.</summary>
    public const string EditTitleDialog = "edit-title-dialog";

    /// <summary>Gets or sets the profile version to report on, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileVersionId { get; set; }

    /// <summary>Gets or sets the selected template, bound from the query string or the posted form.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? TemplateId { get; set; }

    /// <summary>Gets or sets the values of the ticked tree nodes, posted by the form.</summary>
    [BindProperty]
    public List<string> CheckedNodes { get; set; } = [];

    /// <summary>Gets or sets the title typed into the "Save template as" dialog.</summary>
    [BindProperty]
    public string? NewTemplateTitle { get; set; }

    /// <summary>Gets or sets the title typed into the "Edit title" dialog.</summary>
    [BindProperty]
    public string? EditedTemplateTitle { get; set; }

    /// <summary>Gets or sets the message shown after a template change, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets the profile version's content, which supplies the tree, otherwise <see langword="null"/>.</summary>
    public ProfileReportContentDto? ReportContent { get; private set; }

    /// <summary>Gets the tree, otherwise <see langword="null"/> when the page could not be loaded.</summary>
    public TreeViewViewModel? Tree { get; private set; }

    /// <summary>Gets the saved templates, ordered by title.</summary>
    public IReadOnlyList<BespokeReportTemplateSummaryDto> Templates { get; private set; } = [];

    /// <summary>Gets the selected template, otherwise <see langword="null"/>.</summary>
    public BespokeReportTemplateDto? SelectedTemplate { get; private set; }

    /// <summary>Gets a value indicating whether the user is a profile editor, who may save templates and tick the tree.</summary>
    public bool IsProfileEditor { get; private set; }

    /// <summary>Gets the message shown when something went wrong, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the problem with the "Save template as" title, otherwise <see langword="null"/>.</summary>
    public string? NewTitleError { get; private set; }

    /// <summary>Gets the problem with the "Edit title" title, otherwise <see langword="null"/>.</summary>
    public string? EditTitleError { get; private set; }

    /// <summary>Gets the identifier of the dialog to open when the page loads, otherwise <see langword="null"/>.</summary>
    public string? OpenDialog { get; private set; }

    /// <summary>Gets the introduction, which depends on whether the user can save templates.</summary>
    public string IntroText => IsProfileEditor ? EditorIntroText : ViewerIntroText;

    /// <summary>Shows the page, ticking the selected template's nodes.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, or an error status when it cannot be shown.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(fromPostedSelection: false, cancellationToken) ?? Page();

    /// <summary>Saves the ticked nodes as a new template ("Save template as").</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the new template, or the page with what went wrong.</returns>
    public async Task<IActionResult> OnPostSaveAsAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditTemplatesAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        NewTitleError = BespokeTemplateTitle.Validate(NewTemplateTitle);

        if (NewTitleError is not null)
        {
            OpenDialog = SaveAsDialog;

            return await RedisplayAsync(cancellationToken);
        }

        var result = await TryAsync(() => templatesApi.SaveAsAsync(NewTemplateTitle!.Trim(), CheckedNodes, cancellationToken));

        return await CompleteAsync(result, TemplateSavedMessage, SaveAsDialog, cancellationToken);
    }

    /// <summary>Replaces the selected template's ticked nodes ("Update template").</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the template, or the page with what went wrong.</returns>
    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditTemplatesAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (TemplateId is not { } id || id == Guid.Empty)
        {
            return await RedisplayAsync(cancellationToken);
        }

        var result = await TryAsync(() => templatesApi.UpdateAsync(id, CheckedNodes, cancellationToken));

        return await CompleteAsync(result, TemplateSavedMessage, dialog: null, cancellationToken);
    }

    /// <summary>Changes the selected template's title ("Edit title").</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the template, or the page with what went wrong.</returns>
    public async Task<IActionResult> OnPostSaveTitleAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditTemplatesAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (TemplateId is not { } id || id == Guid.Empty)
        {
            return await RedisplayAsync(cancellationToken);
        }

        EditTitleError = BespokeTemplateTitle.Validate(EditedTemplateTitle);

        if (EditTitleError is not null)
        {
            OpenDialog = EditTitleDialog;

            return await RedisplayAsync(cancellationToken);
        }

        var result = await TryAsync(() => templatesApi.RenameAsync(id, EditedTemplateTitle!.Trim(), cancellationToken));

        return await CompleteAsync(result, TemplateTitleSavedMessage, EditTitleDialog, cancellationToken);
    }

    /// <summary>Deletes the selected template.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the page with no template selected, or the page with what went wrong.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditTemplatesAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (TemplateId is not { } id || id == Guid.Empty)
        {
            return await RedisplayAsync(cancellationToken);
        }

        BespokeTemplatesOutcome outcome;

        try
        {
            outcome = await templatesApi.DeleteAsync(id, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.BespokeTemplateCallFailed(exception, ProfileVersionId);
            outcome = BespokeTemplatesOutcome.Error;
        }

        switch (outcome)
        {
            case BespokeTemplatesOutcome.Success:
            case BespokeTemplatesOutcome.NotFound:
                SuccessMessage = TemplateDeletedMessage;

                return RedirectToPage(new { profileVersionId = ProfileVersionId });
            case BespokeTemplatesOutcome.Forbidden:
                logger.BespokeTemplateRequestRefused(ProfileVersionId, outcome);

                return StatusCode(StatusCodes.Status403Forbidden);
            default:
                ErrorMessage = SaveFailureMessage;

                return await RedisplayAsync(cancellationToken);
        }
    }

    /// <summary>Builds the bespoke report for the ticked nodes and downloads it ("Generate bespoke report").</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF, named after the selected template, or the page with what went wrong.</returns>
    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        var selection = BespokeReportNodes.ToSelection(CheckedNodes);

        if (selection.Sections.Count == 0)
        {
            ErrorMessage = NothingSelectedMessage;

            return await RedisplayAsync(cancellationToken);
        }

        try
        {
            var title = await GetSelectedTemplateTitleAsync(cancellationToken);
            var pdf = await pdfBuilder.BuildAsync(ProfileVersionId, selection, title, cancellationToken);

            return File(pdf, "application/pdf", PdfFileName.Build(title ?? ProfileBespokeReportDocuments.DefaultTitle));
        }
        catch (Exception exception) when (exception is PdfRenderException or HttpRequestException or TaskCanceledException or InvalidOperationException or NotSupportedException)
        {
            logger.BespokeReportGenerationFailed(exception, ProfileVersionId);
            ErrorMessage = GenerateFailureMessage;

            return await RedisplayAsync(cancellationToken);
        }
    }

    private async Task<bool> CanEditTemplatesAsync()
    {
        IsProfileEditor = await navigationAuthorization.IsProfileEditorAsync(User);

        return IsProfileEditor;
    }

    /// <summary>The legacy page generated the report under the selected template's title, or the default when there was none.</summary>
    private async Task<string?> GetSelectedTemplateTitleAsync(CancellationToken cancellationToken)
    {
        if (TemplateId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var result = await templatesApi.GetTemplateAsync(id, cancellationToken);

        return result.Template?.Title;
    }

    private async Task<BespokeTemplateResult?> TryAsync(Func<Task<BespokeTemplateResult>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.BespokeTemplateCallFailed(exception, ProfileVersionId);

            return null;
        }
    }

    private async Task<IActionResult> CompleteAsync(BespokeTemplateResult? result, string successMessage, string? dialog, CancellationToken cancellationToken)
    {
        switch (result?.Outcome)
        {
            case BespokeTemplatesOutcome.Success:
                SuccessMessage = successMessage;

                return RedirectToPage(new { profileVersionId = ProfileVersionId, templateId = result.Template!.Id });
            case BespokeTemplatesOutcome.Forbidden:
                logger.BespokeTemplateRequestRefused(ProfileVersionId, result.Outcome);

                return StatusCode(StatusCodes.Status403Forbidden);
            case BespokeTemplatesOutcome.Conflict:
                SetDialogError(dialog, BespokeTemplateTitle.ExistsMessage);
                break;
            case BespokeTemplatesOutcome.NotFound:
                TemplateId = null;
                ErrorMessage = TemplateMissingMessage;
                break;
            case BespokeTemplatesOutcome.Invalid when dialog is not null:
                SetDialogError(dialog, BespokeTemplateTitle.CharactersMessage);
                break;
            default:
                ErrorMessage = SaveFailureMessage;
                break;
        }

        return await RedisplayAsync(cancellationToken);
    }

    private void SetDialogError(string? dialog, string message)
    {
        OpenDialog = dialog;

        if (dialog == SaveAsDialog)
        {
            NewTitleError = message;
        }
        else
        {
            EditTitleError = message;
        }
    }

    /// <summary>Shows the page again after a failed post, keeping the ticked nodes.</summary>
    private async Task<IActionResult> RedisplayAsync(CancellationToken cancellationToken) =>
        await LoadAsync(fromPostedSelection: true, cancellationToken) ?? Page();

    /// <summary>Loads what the page shows; returns a result only when the page cannot be shown.</summary>
    private async Task<IActionResult?> LoadAsync(bool fromPostedSelection, CancellationToken cancellationToken)
    {
        IsProfileEditor = await navigationAuthorization.IsProfileEditorAsync(User);

        try
        {
            var contentResult = await reportsApi.GetContentAsync(ProfileVersionId, cancellationToken);

            if (ContentFailure(contentResult.Outcome) is { } contentFailure)
            {
                return contentFailure;
            }

            ReportContent = contentResult.Content;

            var templatesResult = await templatesApi.GetTemplatesAsync(cancellationToken);

            if (TemplateFailure(templatesResult.Outcome) is { } templatesFailure)
            {
                return templatesFailure;
            }

            Templates = templatesResult.Templates!;

            if (await LoadSelectedTemplateAsync(cancellationToken) is { } selectedFailure)
            {
                return selectedFailure;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.BespokeReportLoadFailed(exception, ProfileVersionId);
            ReportContent = null;
            ErrorMessage = LoadFailureMessage;

            return Page();
        }

        Tree = BespokeReportTree.Build(ReportContent!, fromPostedSelection ? CheckedNodes : SelectedTemplate?.CheckedList ?? []);

        return null;
    }

    private async Task<IActionResult?> LoadSelectedTemplateAsync(CancellationToken cancellationToken)
    {
        if (TemplateId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var result = await templatesApi.GetTemplateAsync(id, cancellationToken);

        // A template deleted in the meantime is not an error: the page shows with none selected, as the legacy page did.
        if (result.Outcome == BespokeTemplatesOutcome.NotFound)
        {
            return null;
        }

        if (TemplateFailure(result.Outcome) is { } failure)
        {
            return failure;
        }

        SelectedTemplate = result.Template;

        return null;
    }

    private IActionResult? ContentFailure(ProfileReportsOutcome outcome)
    {
        switch (outcome)
        {
            case ProfileReportsOutcome.Success:
                return null;
            case ProfileReportsOutcome.NotFound:
                return NotFound();
            case ProfileReportsOutcome.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            default:
                ErrorMessage = LoadFailureMessage;

                return Page();
        }
    }

    private IActionResult? TemplateFailure(BespokeTemplatesOutcome outcome)
    {
        switch (outcome)
        {
            case BespokeTemplatesOutcome.Success:
                return null;
            case BespokeTemplatesOutcome.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            default:
                ReportContent = null;
                ErrorMessage = LoadFailureMessage;

                return Page();
        }
    }
}
