using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Review comments tab of a profile section: its comments and replies, plus adding, replying to,
/// viewing, editing and deleting them. Replaces the legacy <c>EditProfileReviewComments.aspx</c>. Shown
/// only to users allowed to see it (legacy <c>ReviewCommentList.CanGetReviewCommentList()</c>).
/// </summary>
/// <remarks>
/// The legacy page held its open panel in the session; here the panel is chosen by the querystring
/// (<c>mode</c> and <c>commentId</c>), so every state is a plain link and no server state is kept.
/// </remarks>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the current time for the "new!" label; defaults to the system clock.</param>
public class EditProfileReviewCommentsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileReviewCommentsModel> logger,
    TimeProvider? timeProvider = null) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <summary>The empty-state message shown when the section has no review comments.</summary>
    public const string NoCommentsMessage = "There are no review comments to display.";

    /// <summary>The confirmation shown before a comment is deleted.</summary>
    public const string DeleteConfirmation = "Are you sure you want to delete this review comment?";

    /// <summary>The <c>mode</c> querystring value that opens the "Add review comment" panel.</summary>
    public const string AddMode = "add";

    /// <summary>The <c>mode</c> querystring value that opens the "Add review comment" panel as a reply.</summary>
    public const string ReplyMode = "reply";

    /// <summary>The <c>mode</c> querystring value that opens the "Edit review comment" panel.</summary>
    public const string EditMode = "edit";

    private const string DateTimeFormat = "dd/MM/yy HH:mm:ss";
    private const string SavedNotice = "saved";
    private const string DeletedNotice = "deleted";
    private const int MaximumDisplaySubjectLength = 50;
    private const int MaximumTextLength = 255;
    private const int RowVersionLength = 8;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private IReadOnlyList<ProfileReviewCommentDto> allComments = [];

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.ReviewComments;

    /// <summary>Gets or sets the open panel's mode (<c>add</c>, <c>reply</c> or <c>edit</c>); with only a
    /// <see cref="CommentId"/> the read-only view panel opens.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    /// <summary>Gets or sets the comment the panel is about, bound from the querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? CommentId { get; set; }

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the number of comments shown per page.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = NotesModel.PageSizeOptions[0];

    /// <summary>Gets or sets the result of the previous change (<c>saved</c> or <c>deleted</c>), set by the redirect that follows it.</summary>
    [BindProperty(Name = "result", SupportsGet = true)]
    public string? Notice { get; set; }

    /// <summary>Gets or sets the fields posted by the add/edit panel.</summary>
    [BindProperty]
    public ReviewCommentInput Input { get; set; } = new();

    /// <summary>Gets the current page of comments, each reply indented by its thread depth.</summary>
    public IReadOnlyList<ProfileReviewCommentView> ReviewComments { get; private set; } = [];

    /// <summary>Gets the total number of comments, before paging.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets which panel is open beneath the table.</summary>
    public ReviewCommentPanel Panel { get; private set; }

    /// <summary>Gets the comment shown in the read-only panel.</summary>
    public ProfileReviewCommentDetailView? Detail { get; private set; }

    /// <summary>Gets the add/edit panel's heading.</summary>
    public string PanelHeading => Panel == ReviewCommentPanel.Edit ? "Edit review comment" : "Add review comment";

    /// <summary>Gets the validation messages for the add/edit panel.</summary>
    public IReadOnlyList<string> ValidationErrors { get; private set; } = [];

    /// <summary>Gets the result message shown above the table, if any.</summary>
    public string? ActionMessage { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ActionMessage"/> reports a failure.</summary>
    public bool ActionMessageIsError { get; private set; }

    /// <summary>Gets a value indicating whether the user may add a comment (legacy <c>CanAddReviewComment</c>).</summary>
    public bool CanAdd => CanEditProfileSection;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        ApplyNotice();
        OpenRequestedPanel();

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        Panel = Input.CommentId is null ? ReviewCommentPanel.Add : ReviewCommentPanel.Edit;

        if (!CanAdd)
        {
            SetFailure("Review comment save failed: You cannot change review comments on this version.");
            return Page();
        }

        NormaliseInput();
        ValidationErrors = Validate();

        if (ValidationErrors.Count > 0)
        {
            return Page();
        }

        var result = await SaveAsync(cancellationToken);

        if (result.Outcome == ReviewCommentOutcome.Success)
        {
            return RedirectAfterChange(SavedNotice);
        }

        SetFailure($"Review comment save failed: {result.ErrorMessage}", result.Outcome);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        CommentId = commentId;
        var result = await DeleteAsync(commentId, cancellationToken);

        if (result.Outcome == ReviewCommentOutcome.Success)
        {
            return RedirectAfterChange(DeletedNotice);
        }

        SetFailure($"Review comment deletion failed: {result.ErrorMessage}", result.Outcome);
        OpenViewPanel(allComments.FirstOrDefault(comment => comment.Id == commentId));

        return Page();
    }

    /// <summary>Builds the URL of the table (no panel open), optionally on another results page.</summary>
    /// <param name="page">The results page, or <see langword="null"/> for the current one.</param>
    /// <returns>The page URL.</returns>
    public string? BuildListUrl(int? page = null) => BuildUrl(null, null, page ?? PageNumber);

    /// <summary>Builds the URL that opens the "Add review comment" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildAddUrl() => BuildUrl(AddMode, null, PageNumber);

    /// <summary>Builds the URL that opens a comment's read-only panel.</summary>
    /// <param name="commentId">The comment.</param>
    /// <returns>The page URL.</returns>
    public string? BuildViewUrl(Guid commentId) => BuildUrl(null, commentId, PageNumber);

    /// <summary>Builds the URL that opens the reply panel for a comment.</summary>
    /// <param name="commentId">The comment being replied to.</param>
    /// <returns>The page URL.</returns>
    public string? BuildReplyUrl(Guid commentId) => BuildUrl(ReplyMode, commentId, PageNumber);

    /// <summary>Builds the URL that opens the edit panel for a comment.</summary>
    /// <param name="commentId">The comment being edited.</param>
    /// <returns>The page URL.</returns>
    public string? BuildEditUrl(Guid commentId) => BuildUrl(EditMode, commentId, PageNumber);

    private string? BuildUrl(string? mode, Guid? commentId, int page) =>
        Url.Page(
            ProfileSectionTabs.PagePath(ProfileSectionTab.ReviewComments),
            new { profileId = ProfileId, section = CurrentSection?.Id, mode, commentId, pageNumber = page, pageSize = PageSize });

    private async Task<IActionResult?> InitialiseAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (!IsReady)
        {
            return Page();
        }

        if (!CanViewReviewComments)
        {
            return NotFound();
        }

        await LoadReviewCommentsAsync(cancellationToken);

        return null;
    }

    /// <summary>Loads the comments, resiliently: a failure degrades to an empty list rather than failing the page.</summary>
    private async Task LoadReviewCommentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            allComments = await ProfileSectionsApi.GetProfileSectionReviewCommentsAsync(CurrentProfileVersionId, CurrentSection!.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileReviewCommentsLoadFailed(exception, ProfileId);
            allComments = [];
        }

        TotalRecords = allComments.Count;

        var pageSize = ResolvePageSize();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        var now = clock.GetLocalNow().DateTime;

        ReviewComments =
        [
            .. allComments
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(comment => new ProfileReviewCommentView(
                    comment.Id,
                    comment.Level,
                    TruncateSubject(comment.CommentSubject),
                    comment.CommentReference,
                    comment.UserFullName,
                    comment.CommentDate.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                    comment.CommentDate > now.AddHours(-24)))
        ];
    }

    private int ResolvePageSize() =>
        int.TryParse(PageSize, out var parsed) && parsed > 0 ? parsed : int.Parse(NotesModel.PageSizeOptions[0], CultureInfo.InvariantCulture);

    /// <summary>Truncates a subject for the grid, matching the legacy <c>FormatSubject()</c>.</summary>
    private static string TruncateSubject(string subject) =>
        subject.Length > MaximumDisplaySubjectLength ? subject[..MaximumDisplaySubjectLength] + "..." : subject;

    private void ApplyNotice()
    {
        if (string.Equals(Notice, SavedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = "Review comment save succeeded";
        }
        else if (string.Equals(Notice, DeletedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = "Review comment deletion succeeded";
        }
    }

    private void OpenRequestedPanel()
    {
        var selected = CommentId.HasValue ? allComments.FirstOrDefault(comment => comment.Id == CommentId.Value) : null;

        if (string.Equals(Mode, AddMode, StringComparison.OrdinalIgnoreCase))
        {
            OpenAddPanel(null, isReply: false);
        }
        else if (string.Equals(Mode, ReplyMode, StringComparison.OrdinalIgnoreCase))
        {
            OpenAddPanel(selected, isReply: true);
        }
        else if (string.Equals(Mode, EditMode, StringComparison.OrdinalIgnoreCase))
        {
            OpenEditPanel(selected);
        }
        else
        {
            OpenViewPanel(selected);
        }
    }

    /// <summary>Opens the add panel; for a reply, the subject is prefixed "Re: " (unless it already is) and the reference is copied.</summary>
    private void OpenAddPanel(ProfileReviewCommentDto? parent, bool isReply)
    {
        if (!CanAdd || (isReply && parent is null))
        {
            return;
        }

        Panel = ReviewCommentPanel.Add;
        Input = parent is null
            ? new ReviewCommentInput()
            : new ReviewCommentInput { Subject = ReplySubject(parent.CommentSubject), Reference = parent.CommentReference, ParentId = parent.Id };
    }

    private static string ReplySubject(string subject) =>
        subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;

    private void OpenEditPanel(ProfileReviewCommentDto? comment)
    {
        if (comment is not { CanEdit: true } || !CanAdd)
        {
            return;
        }

        Panel = ReviewCommentPanel.Edit;
        Input = new ReviewCommentInput
        {
            Subject = comment.CommentSubject,
            Reference = comment.CommentReference,
            CommentText = comment.CommentText,
            CommentId = comment.Id,
            LastUpdated = Convert.ToBase64String(comment.LastUpdated)
        };
    }

    private void OpenViewPanel(ProfileReviewCommentDto? comment)
    {
        if (comment is null)
        {
            return;
        }

        Panel = ReviewCommentPanel.View;
        Detail = new ProfileReviewCommentDetailView(
            comment.Id,
            comment.CommentSubject,
            comment.CommentReference,
            ReviewCommentHtml.ToDisplayHtml(comment.CommentText),
            comment.CanEdit && CanAdd,
            comment.CanDelete && CanAdd);
    }

    private void NormaliseInput()
    {
        Input.Subject = (Input.Subject ?? string.Empty).Trim();
        Input.Reference = (Input.Reference ?? string.Empty).Trim();
        Input.CommentText = ReviewCommentHtml.Sanitize(Input.CommentText);
    }

    /// <summary>Applies the legacy panel's validation rules and messages.</summary>
    private List<string> Validate()
    {
        var errors = new List<string>();

        if (Input.Subject.Length == 0)
        {
            errors.Add("You must enter a subject or cancel");
        }
        else if (Input.Subject.Length > MaximumTextLength)
        {
            errors.Add("A subject must be no longer than 255 characters");
        }

        if (Input.Reference.Length == 0)
        {
            errors.Add("You must enter a reference or cancel");
        }
        else if (Input.Reference.Length > MaximumTextLength)
        {
            errors.Add("A reference must be no longer than 255 characters");
        }

        if (ProfileTitleHtmlFormatter.ToPlainText(Input.CommentText).Trim().Length == 0)
        {
            errors.Add("You must enter a comment or cancel");
        }

        return errors;
    }

    private async Task<ReviewCommentResult> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Input.CommentId is null)
            {
                return await ProfileSectionsApi.AddProfileReviewCommentAsync(
                    CurrentProfileVersionId, CurrentSection!.Id, Input.ParentId, Input.Subject, Input.Reference, Input.CommentText, cancellationToken);
            }

            var lastUpdated = DecodeRowVersion(Input.LastUpdated);

            if (lastUpdated is null)
            {
                return new ReviewCommentResult { Outcome = ReviewCommentOutcome.ValidationFailed, ErrorMessage = "The comment could not be identified. Reopen it and try again." };
            }

            return await ProfileSectionsApi.UpdateProfileReviewCommentAsync(
                CurrentProfileVersionId,
                CurrentSection!.Id,
                Input.CommentId.Value,
                new ReviewCommentContent(Input.Subject, Input.Reference, Input.CommentText),
                lastUpdated,
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileReviewCommentSaveFailed(exception, ProfileId);

            return Failure("We could not save the review comment. Try again later.");
        }
    }

    private async Task<ReviewCommentResult> DeleteAsync(Guid commentId, CancellationToken cancellationToken)
    {
        if (!CanAdd)
        {
            return Failure("You cannot change review comments on this version.");
        }

        try
        {
            return await ProfileSectionsApi.DeleteProfileReviewCommentAsync(CurrentProfileVersionId, CurrentSection!.Id, commentId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileReviewCommentDeleteFailed(exception, ProfileId);

            return Failure("We could not delete the review comment. Try again later.");
        }
    }

    private static ReviewCommentResult Failure(string message) =>
        new() { Outcome = ReviewCommentOutcome.Error, ErrorMessage = message };

    private static byte[]? DecodeRowVersion(string value)
    {
        Span<byte> buffer = stackalloc byte[RowVersionLength];

        return Convert.TryFromBase64String(value, buffer, out var written) && written == RowVersionLength ? buffer.ToArray() : null;
    }

    private void SetFailure(string message, ReviewCommentOutcome? outcome = null)
    {
        ActionMessage = message;
        ActionMessageIsError = true;

        if (outcome is not null and not ReviewCommentOutcome.Error)
        {
            Logger.ProfileReviewCommentChangeRefused(ProfileId, outcome.Value);
        }
    }

    /// <summary>Redirects after a successful change (Post-Redirect-Get), so a refresh cannot repeat it. The table
    /// returns to its first page, where a new comment appears.</summary>
    private RedirectToPageResult RedirectAfterChange(string notice) =>
        RedirectToPage(
            ProfileSectionTabs.PagePath(ProfileSectionTab.ReviewComments),
            new { profileId = ProfileId, section = CurrentSection!.Id, pageSize = PageSize, result = notice });
}
