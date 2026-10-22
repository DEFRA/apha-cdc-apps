using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Further information tab of a profile section: its sources of further information. A source is added, picked from the
/// version's master list, viewed (to change which of the section's questions it is linked to) or deleted (unlinked from the
/// section; it stays in the master list). Replaces the legacy <c>EditProfileFurtherInformation.aspx</c> and the
/// <c>EditNoteUI</c> control it hosts.
/// </summary>
/// <remarks>
/// The legacy control held its note lists and open panel in the session; here the panel is chosen by the querystring
/// (<c>mode</c> and <c>noteId</c>), so every state is a plain link and no server state is kept. The page is addressed by the
/// profile and the section; the profile's current version, which the legacy page took from its <c>ProfileVersionId</c>
/// querystring value, is resolved from the profile.
/// </remarks>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileFurtherInformationModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileFurtherInformationModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <summary>The <c>mode</c> querystring value that opens the "Add" panel.</summary>
    public const string AddMode = "add";

    /// <summary>The <c>mode</c> querystring value that opens the "Select" panel.</summary>
    public const string SelectMode = "select";

    /// <summary>The <c>sortColumn</c> value that sorts the table by the note text.</summary>
    public const string ReferenceSortColumn = "Reference";

    /// <summary>The <c>sortColumn</c> value that sorts the table by the question reference.</summary>
    public const string QuestionReferenceSortColumn = "QuestionReference";

    /// <summary>The id of the Add panel's text box, which its error message links to.</summary>
    public const string NoteTextFieldId = "Input_NoteText";

    /// <summary>The id of the Select panel's drop-down, which its error message links to.</summary>
    public const string NoteSelectFieldId = "NoteId";

    /// <summary>The id of the first question checkbox, which the "no question ticked" error message links to.</summary>
    public const string FirstQuestionFieldId = "Input_QuestionIds_0";

    private const string ViewMode = "view";
    private const string SavedNotice = "saved";
    private const string DeletedNotice = "deleted";
    private const string FallbackName = "Source of further information";
    private const string FallbackPluralName = "Sources of further information";
    private const string AscendingDirection = "Ascending";
    private const string DescendingDirection = "Descending";

    private ProfileNoteTypeDto? noteType;
    private bool notesLoaded;
    private IReadOnlyList<ProfileNoteDto> sectionNotes = [];
    private IReadOnlyList<ProfileNoteDto> availableNotes = [];
    private IReadOnlyList<ProfileQuestionInfoDto>? questions;
    private ProfileNoteDto? panelNote;

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.FurtherInformation;

    /// <inheritdoc />
    public override bool HasFormErrors => FormErrors.Count > 0 || ActionMessageIsError;

    /// <inheritdoc />
    public override int ListPageNumber => PageNumber;

    /// <inheritdoc />
    public override int ListTotalPages => TotalPages;

    /// <summary>Gets or sets the open panel (<c>add</c> or <c>select</c>); with only a <see cref="NoteId"/> the View panel opens.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    /// <summary>Gets or sets the note the panel is about, bound from the querystring: the one chosen in the Select panel, or the one viewed.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? NoteId { get; set; }

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the number of notes shown per page.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = NotesModel.PageSizeOptions[0];

    /// <summary>Gets or sets which column the table is sorted by: <see cref="ReferenceSortColumn"/> or <see cref="QuestionReferenceSortColumn"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = ReferenceSortColumn;

    /// <summary>Gets or sets the sort direction: "Ascending" or "Descending".</summary>
    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = AscendingDirection;

    /// <summary>Gets or sets the result of the previous change (<c>saved</c> or <c>deleted</c>), set by the redirect that follows it.</summary>
    [BindProperty(Name = "result", SupportsGet = true)]
    public string? Notice { get; set; }

    /// <summary>Gets or sets the fields posted by the panel.</summary>
    [BindProperty]
    public ProfileNoteInput Input { get; set; } = new();

    /// <summary>Gets the singular name of the note type, as CDC.Api names it (for example "Source of further information").</summary>
    public string NoteTypeName { get; private set; } = FallbackName;

    /// <summary>Gets the plural name of the note type (for example "Sources of further information").</summary>
    public string NoteTypePluralName { get; private set; } = FallbackPluralName;

    /// <summary>Gets the label of the "Add" button (legacy <c>lblAddNote</c>).</summary>
    public string AddLabel => $"Add a new {NoteTypeName.ToLowerInvariant()}:";

    /// <summary>Gets the label of the "Select" button (legacy <c>lblSelectNote</c>).</summary>
    public string SelectLabel => $"Select a {NoteTypeName.ToLowerInvariant()} from the master list:";

    /// <summary>Gets the message shown when the section has no sources of further information (legacy <c>EmptyDataText</c>).</summary>
    public string NoNotesMessage => ProfileNoteDisplayHelper.BuildEmptyMessage(NoteTypePluralName);

    /// <summary>Gets the confirmation shown before a source is unlinked from the section.</summary>
    public string DeleteConfirmation => $"Are you sure you want to delete the association to this {NoteTypeName.ToLowerInvariant()}?";

    /// <summary>Gets the open panel's heading.</summary>
    public string PanelHeading => Panel switch
    {
        ProfileNotePanel.Add => $"Add a {NoteTypeName.ToLowerInvariant()}",
        ProfileNotePanel.Select => $"Select a {NoteTypeName.ToLowerInvariant()}",
        ProfileNotePanel.View => $"View a {NoteTypeName.ToLowerInvariant()}",
        _ => string.Empty
    };

    /// <summary>Gets the current page of the section's sources.</summary>
    public IReadOnlyList<ProfileNoteGridRow> Rows { get; private set; } = [];

    /// <summary>Gets the total number of the section's sources, before paging.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the user may add, select, view and delete sources (legacy <c>NoteList.CanEditNoteList()</c>).</summary>
    public bool CanEdit => CanEditProfileSection && notesLoaded && noteType is not null;

    /// <summary>Gets a value indicating whether the "Select" button is shown: the user may edit and the master list holds a
    /// source that is not yet linked to this section.</summary>
    public bool CanSelect => CanEdit && availableNotes.Count > 0;

    /// <summary>Gets which panel is open beneath the table.</summary>
    public ProfileNotePanel Panel { get; private set; }

    /// <summary>Gets the master-list sources that are not yet linked to this section, for the Select panel's drop-down.</summary>
    public IReadOnlyList<ProfileNoteChoice> AvailableNoteChoices =>
        [.. availableNotes.Select(note => new ProfileNoteChoice(note.Id, ProfileNoteDisplayHelper.ToSlimText(note.NoteText), note.Id == Input.NoteId))];

    /// <summary>Gets the full text of the source chosen in the Select panel, or shown in the View panel, without paragraph tags;
    /// <see langword="null"/> when there is none.</summary>
    public string? PanelNoteHtml => panelNote is null ? null : ProfileNoteDisplayHelper.StripParagraphTags(panelNote.NoteText);

    /// <summary>Gets the section's questions, each ticked when the source is linked to it.</summary>
    public IReadOnlyList<QuestionReferenceChoice> QuestionChoices { get; private set; } = [];

    /// <summary>Gets the validation messages of the open panel.</summary>
    public IReadOnlyList<ProfileNoteFormError> FormErrors { get; private set; } = [];

    /// <summary>Gets the result message shown above the table, if any.</summary>
    public string? ActionMessage { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ActionMessage"/> reports a failure.</summary>
    public bool ActionMessageIsError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        ApplyNotice();
        BuildRows();
        await OpenRequestedPanelAsync(cancellationToken);

        return Page();
    }

    /// <summary>Handles the panel's "OK" button: adds the new source, or links the selected or viewed one to the ticked questions.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the table after a change, or the page with the panel open when the submission was not accepted.</returns>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        var panel = ParsePanel(Input.Mode);

        if (panel == ProfileNotePanel.None)
        {
            return BadRequest();
        }

        BuildRows();

        if (!CanEdit)
        {
            SetFailureUnlessAlreadyReported($"{NoteTypeName} save failed: You cannot change a {NoteTypeName.ToLowerInvariant()} on this version.");

            return Page();
        }

        if (!await ShowPanelAsync(panel, cancellationToken))
        {
            return Page();
        }

        if (panel == ProfileNotePanel.View && panelNote is null)
        {
            Panel = ProfileNotePanel.None;
            SetFailure($"{NoteTypeName} save failed: The {NoteTypeName.ToLowerInvariant()} no longer exists.");

            return Page();
        }

        NormaliseInput(panel);
        FormErrors = Validate(panel);

        if (FormErrors.Count > 0)
        {
            return Page();
        }

        var result = await SaveAsync(panel, cancellationToken);

        if (result.Outcome == ProfileNoteChangeOutcome.Success)
        {
            return RedirectAfterChange(SavedNotice);
        }

        SetFailure($"{NoteTypeName} save failed: {result.ErrorMessage}", result.Outcome);

        return Page();
    }

    /// <summary>Handles a row's "Delete" confirmation: unlinks the source from every question in the section. The source stays
    /// in the master list, so it can be selected again.</summary>
    /// <param name="noteId">The source to unlink.</param>
    /// <param name="lastUpdated">The source's Base64 row version, read when the table was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the table after the change, or the page with the reason it failed.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(Guid noteId, string? lastUpdated, CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        BuildRows();

        var lowerName = NoteTypeName.ToLowerInvariant();

        if (!CanEdit)
        {
            SetFailureUnlessAlreadyReported($"{NoteTypeName} deletion failed: You cannot change a {lowerName} on this version.");

            return Page();
        }

        var note = sectionNotes.FirstOrDefault(candidate => candidate.Id == noteId);
        var rowVersion = ProfileNoteChangeBuilder.DecodeRowVersion(lastUpdated);

        if (note is null || rowVersion is null)
        {
            SetFailure(note is null
                ? $"{NoteTypeName} deletion failed: The {lowerName} no longer exists."
                : $"{NoteTypeName} deletion failed: The {lowerName} could not be identified. Reload the page and try again.");

            return Page();
        }

        var update = ProfileNoteChangeBuilder.BuildRemoval(note, rowVersion, CurrentSection!.Id);
        var result = update is null
            ? new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Success }
            : await ChangeAsync([], [update], cancellationToken);

        if (result.Outcome == ProfileNoteChangeOutcome.Success)
        {
            return RedirectAfterChange(DeletedNotice);
        }

        SetFailure($"{NoteTypeName} deletion failed: {result.ErrorMessage}", result.Outcome);

        return Page();
    }

    /// <summary>Handles the panel's "Tick all" button: ticks every question, keeping everything else entered.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page with the panel open.</returns>
    public Task<IActionResult> OnPostTickAllAsync(CancellationToken cancellationToken) => RedisplayPanelAsync(tickAll: true, cancellationToken);

    /// <summary>Handles the panel's "Untick all" button: unticks every question, keeping everything else entered.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page with the panel open.</returns>
    public Task<IActionResult> OnPostUntickAllAsync(CancellationToken cancellationToken) => RedisplayPanelAsync(tickAll: false, cancellationToken);

    /// <summary>Builds the URL of the table with no panel open, optionally on another results page.</summary>
    /// <param name="page">The results page, or <see langword="null"/> for the current one.</param>
    /// <returns>The page URL.</returns>
    public string? BuildListUrl(int? page = null) => BuildUrl(null, null, page ?? PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens the "Add" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildAddUrl() => BuildUrl(AddMode, null, PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens the "Select" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildSelectUrl() => BuildUrl(SelectMode, null, PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens a source's "View" panel.</summary>
    /// <param name="noteId">The source.</param>
    /// <returns>The page URL.</returns>
    public string? BuildViewUrl(Guid noteId) => BuildUrl(null, noteId, PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL of a sortable column header: ascending when the table is not yet sorted by that column,
    /// otherwise toggling the direction. Returns to the first page.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The page URL.</returns>
    public string? BuildSortUrl(string column)
    {
        var direction = string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SortDirection, AscendingDirection, StringComparison.OrdinalIgnoreCase)
                ? DescendingDirection
                : AscendingDirection;

        return BuildUrl(null, null, 1, column, direction);
    }

    /// <summary>Gets the <c>aria-sort</c> value of a sortable column header.</summary>
    /// <param name="column">The column.</param>
    /// <returns>"none" unless the table is sorted by the column.</returns>
    public string GetAriaSort(string column)
    {
        if (!string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        return string.Equals(SortDirection, DescendingDirection, StringComparison.OrdinalIgnoreCase) ? "descending" : "ascending";
    }

    private static ProfileNotePanel ParsePanel(string? mode) => mode?.ToLowerInvariant() switch
    {
        AddMode => ProfileNotePanel.Add,
        SelectMode => ProfileNotePanel.Select,
        ViewMode => ProfileNotePanel.View,
        _ => ProfileNotePanel.None
    };

    private static string ModeOf(ProfileNotePanel panel) => panel switch
    {
        ProfileNotePanel.Add => AddMode,
        ProfileNotePanel.Select => SelectMode,
        _ => ViewMode
    };

    private string? BuildUrl(string? mode, Guid? noteId, int page, string sortColumn, string sortDirection) =>
        Url.Page(
            ProfileSectionTabs.PagePath(ProfileSectionTab.FurtherInformation),
            new { profileId = ProfileId, section = CurrentSection?.Id, mode, noteId, pageNumber = page, pageSize = PageSize, sortColumn, sortDirection });

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

        await LoadNotesAsync(cancellationToken);

        return null;
    }

    /// <summary>Loads the section's notes and, for a user who may edit, the version's master list, resiliently: a failure
    /// degrades to an empty table, with editing switched off, rather than failing the page.</summary>
    private async Task LoadNotesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var noteTypes = await ProfileSectionsApi.GetProfileNoteTypesAsync(cancellationToken);

            noteType = noteTypes.FirstOrDefault(type =>
                ProfileNoteDisplayHelper.NormaliseNoteTypeName(type.Name) == ProfileNoteDisplayHelper.SourceOfFurtherInformationType);

            if (noteType is null)
            {
                return;
            }

            NoteTypeName = noteType.Name;
            NoteTypePluralName = string.IsNullOrEmpty(noteType.PluralName) ? FallbackPluralName : noteType.PluralName;

            sectionNotes = await ProfileSectionsApi.GetProfileNotesBySectionAsync(CurrentProfileVersionId, CurrentSection!.Id, noteType.Id, cancellationToken);

            if (CanEditProfileSection)
            {
                var versionNotes = await ProfileSectionsApi.GetProfileNotesByVersionAsync(CurrentProfileVersionId, noteType.Id, cancellationToken);
                var linkedIds = sectionNotes.Select(note => note.Id).ToHashSet();
                availableNotes = [.. versionNotes.Where(note => !linkedIds.Contains(note.Id))];
            }

            notesLoaded = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileNotesLoadFailed(exception, ProfileId);
            sectionNotes = [];
            availableNotes = [];
            SetFailure($"We could not load the {NoteTypePluralName.ToLowerInvariant()}. Try again later.");
        }
    }

    private void ApplyNotice()
    {
        if (ActionMessage is not null)
        {
            return;
        }

        if (string.Equals(Notice, SavedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = $"{NoteTypeName} save succeeded";
        }
        else if (string.Equals(Notice, DeletedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = $"{NoteTypeName} deletion succeeded";
        }
    }

    /// <summary>Sorts and pages the section's notes into <see cref="Rows"/>, matching the legacy grid's default of the plain
    /// text ascending.</summary>
    private void BuildRows()
    {
        var section = CurrentSection!;

        var allRows = sectionNotes
            .Select(note => new ProfileNoteGridRow(
                note.Id,
                note.NoteText,
                ProfileTitleHtmlFormatter.ToPlainText(note.NoteText),
                ProfileNoteDisplayHelper.BuildQuestionReferenceText(note, section),
                ProfileNoteDisplayHelper.ToSlimText(note.NoteText),
                Convert.ToBase64String(note.LastUpdated)))
            .ToList();

        var sortByQuestionReference = string.Equals(SortColumn, QuestionReferenceSortColumn, StringComparison.OrdinalIgnoreCase);
        var descending = string.Equals(SortDirection, DescendingDirection, StringComparison.OrdinalIgnoreCase);

        SortColumn = sortByQuestionReference ? QuestionReferenceSortColumn : ReferenceSortColumn;
        SortDirection = descending ? DescendingDirection : AscendingDirection;

        Func<ProfileNoteGridRow, string> sortKey = sortByQuestionReference ? row => row.QuestionReferenceDisplay : row => row.PlainText;
        var sorted = descending
            ? allRows.OrderByDescending(sortKey, StringComparer.OrdinalIgnoreCase)
            : allRows.OrderBy(sortKey, StringComparer.OrdinalIgnoreCase);

        var pageSize = ResolvePageSize();

        TotalRecords = allRows.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Rows = [.. sorted.Skip((PageNumber - 1) * pageSize).Take(pageSize)];
    }

    /// <summary>Resolves the page size to one of the offered options, and keeps <see cref="PageSize"/> to match.</summary>
    private int ResolvePageSize()
    {
        if (!NotesModel.PageSizeOptions.Contains(PageSize))
        {
            PageSize = NotesModel.PageSizeOptions[0];
        }

        return int.Parse(PageSize, CultureInfo.InvariantCulture);
    }

    /// <summary>Opens the panel the querystring asks for, when the user may edit and it applies.</summary>
    private async Task OpenRequestedPanelAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return;
        }

        if (string.Equals(Mode, AddMode, StringComparison.OrdinalIgnoreCase))
        {
            await ShowPanelAsync(ProfileNotePanel.Add, cancellationToken);
        }
        else if (string.Equals(Mode, SelectMode, StringComparison.OrdinalIgnoreCase))
        {
            if (CanSelect)
            {
                Input.NoteId = NoteId;
                await ShowPanelAsync(ProfileNotePanel.Select, cancellationToken);
            }
        }
        else if (sectionNotes.FirstOrDefault(note => note.Id == NoteId) is { } viewed)
        {
            // Legacy opened the note with the questions it is already linked to ticked.
            Input.NoteId = viewed.Id;
            Input.QuestionIds = [.. viewed.QuestionReferences.Where(reference => reference.ProfileSectionId == CurrentSection!.Id).Select(reference => reference.ProfileQuestionId)];
            await ShowPanelAsync(ProfileNotePanel.View, cancellationToken);
        }
    }

    /// <summary>Opens a panel: loads the section's questions, finds the note it is about and builds the checkboxes from what
    /// <see cref="Input"/> holds. Ticks that are not one of the section's questions are dropped.</summary>
    /// <returns><see langword="false"/> when the questions could not be loaded, so there is no panel to show.</returns>
    private async Task<bool> ShowPanelAsync(ProfileNotePanel panel, CancellationToken cancellationToken)
    {
        var sectionQuestions = await LoadQuestionsAsync(cancellationToken);

        if (sectionQuestions is null)
        {
            return false;
        }

        panelNote = panel switch
        {
            ProfileNotePanel.Select => availableNotes.FirstOrDefault(note => note.Id == Input.NoteId),
            ProfileNotePanel.View => sectionNotes.FirstOrDefault(note => note.Id == Input.NoteId),
            _ => null
        };

        Panel = panel;
        Input.Mode = ModeOf(panel);

        if (panelNote is not null && string.IsNullOrEmpty(Input.LastUpdated))
        {
            Input.LastUpdated = Convert.ToBase64String(panelNote.LastUpdated);
        }

        var questionIds = sectionQuestions.Select(question => question.Id).ToHashSet();
        Input.QuestionIds = [.. Input.QuestionIds.Where(questionIds.Contains).Distinct()];

        QuestionChoices =
        [
            .. sectionQuestions
                .OrderBy(question => question.QuestionNumber)
                .Select(question => new QuestionReferenceChoice(
                    question.Id,
                    $"{CurrentSection!.SectionNumber}.{question.QuestionNumber}",
                    question.Name,
                    Input.QuestionIds.Contains(question.Id)))
        ];

        return true;
    }

    /// <summary>Loads the section's questions once, resiliently: a failure is reported on the page rather than failing it.</summary>
    private async Task<IReadOnlyList<ProfileQuestionInfoDto>?> LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        if (questions is not null)
        {
            return questions;
        }

        try
        {
            questions = await ProfileSectionsApi.GetProfileQuestionInfoListAsync(CurrentSection!.Id, cancellationToken);

            return questions;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionQuestionsLoadFailed(exception, ProfileId, CurrentSection!.Id);
            SetFailure("We could not load the questions for this section. Try again later.");

            return null;
        }
    }

    private async Task<IActionResult> RedisplayPanelAsync(bool tickAll, CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        var panel = ParsePanel(Input.Mode);

        if (panel == ProfileNotePanel.None)
        {
            return BadRequest();
        }

        BuildRows();

        if (!CanEdit)
        {
            return Page();
        }

        var sectionQuestions = await LoadQuestionsAsync(cancellationToken);

        if (sectionQuestions is null)
        {
            return Page();
        }

        Input.QuestionIds = tickAll ? [.. sectionQuestions.Select(question => question.Id)] : [];
        await ShowPanelAsync(panel, cancellationToken);

        return Page();
    }

    /// <summary>Sanitises a new source's text, which comes from a link-only rich text editor and so is untrusted. The other panels post none.</summary>
    private void NormaliseInput(ProfileNotePanel panel) =>
        Input.NoteText = panel == ProfileNotePanel.Add ? ReviewCommentHtml.Sanitize(Input.NoteText) : string.Empty;

    /// <summary>Applies the legacy panel's rules: a new source needs text, a selected one must be chosen, and at least one question
    /// must be ticked.</summary>
    private List<ProfileNoteFormError> Validate(ProfileNotePanel panel)
    {
        var errors = new List<ProfileNoteFormError>();
        var lowerName = NoteTypeName.ToLowerInvariant();

        if (panel == ProfileNotePanel.Add && ProfileTitleHtmlFormatter.ToPlainText(Input.NoteText).Length == 0)
        {
            errors.Add(new ProfileNoteFormError(NoteTextFieldId, $"Enter a {lowerName}"));
        }
        else if (panel == ProfileNotePanel.Select && panelNote is null)
        {
            errors.Add(new ProfileNoteFormError(NoteSelectFieldId, $"Select a {lowerName}"));
        }

        if (Input.QuestionIds.Count == 0)
        {
            errors.Add(new ProfileNoteFormError(FirstQuestionFieldId, "Select at least one question"));
        }

        return errors;
    }

    private async Task<ProfileNoteChangeResult> SaveAsync(ProfileNotePanel panel, CancellationToken cancellationToken)
    {
        var sectionId = CurrentSection!.Id;

        if (panel == ProfileNotePanel.Add)
        {
            var insert = ProfileNoteChangeBuilder.BuildInsert(Guid.NewGuid(), Input.NoteText, sectionId, Input.QuestionIds);

            return await ChangeAsync([insert], [], cancellationToken);
        }

        var rowVersion = ProfileNoteChangeBuilder.DecodeRowVersion(Input.LastUpdated);

        if (rowVersion is null)
        {
            return new ProfileNoteChangeResult
            {
                Outcome = ProfileNoteChangeOutcome.ValidationFailed,
                ErrorMessage = $"The {NoteTypeName.ToLowerInvariant()} could not be identified. Reopen it and try again."
            };
        }

        // Nothing to save when the ticks already match the note's links, as the legacy list had no dirty note then.
        var update = ProfileNoteChangeBuilder.BuildUpdate(panelNote!, rowVersion, sectionId, Input.QuestionIds);

        return update is null
            ? new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Success }
            : await ChangeAsync([], [update], cancellationToken);
    }

    private async Task<ProfileNoteChangeResult> ChangeAsync(
        IReadOnlyList<ProfileNoteInsert> inserts,
        IReadOnlyList<ProfileNoteUpdate> updates,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ProfileSectionsApi.UpdateProfileNotesAsync(CurrentProfileVersionId, noteType!.Id, inserts, updates, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileNoteChangeFailed(exception, ProfileId);

            return new ProfileNoteChangeResult
            {
                Outcome = ProfileNoteChangeOutcome.Error,
                ErrorMessage = "We could not save the change. Try again later."
            };
        }
    }

    private void SetFailure(string message, ProfileNoteChangeOutcome? outcome = null)
    {
        ActionMessage = message;
        ActionMessageIsError = true;

        if (outcome is not null and not ProfileNoteChangeOutcome.Error)
        {
            Logger.ProfileNoteChangeRefused(ProfileId, outcome.Value);
        }
    }

    /// <summary>Reports a failure, unless the page has already reported why it cannot be used (for example a failed load).</summary>
    private void SetFailureUnlessAlreadyReported(string message)
    {
        if (ActionMessage is null)
        {
            SetFailure(message);
        }
    }

    /// <summary>Redirects after a successful change (Post-Redirect-Get), so a refresh cannot repeat it. The sort and the page are kept.</summary>
    private RedirectToPageResult RedirectAfterChange(string notice) =>
        RedirectToPage(
            ProfileSectionTabs.PagePath(ProfileSectionTab.FurtherInformation),
            new { profileId = ProfileId, section = CurrentSection!.Id, pageNumber = PageNumber, pageSize = PageSize, sortColumn = SortColumn, sortDirection = SortDirection, result = notice });
}
