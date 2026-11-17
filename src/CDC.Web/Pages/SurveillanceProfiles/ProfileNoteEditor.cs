using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>How an editor finished a request, which the page turns into its response.</summary>
public enum ProfileNoteOutcome
{
    /// <summary>Show the page again, with the panel and the messages the editor set.</summary>
    Redisplay,

    /// <summary>The request named no panel the editor offers.</summary>
    BadRequest,

    /// <summary>A note was added, linked or unlinked from questions: redirect to the table.</summary>
    Saved,

    /// <summary>A note was unlinked from the section: redirect to the table.</summary>
    Deleted
}

/// <summary>
/// One kind of note (scientific paper reference, legislative reference or source of further information) on a tab of a profile section:
/// its table, and the panels that add a note, pick one from the version's master list, or view one to change which of the section's
/// questions it is linked to. Deleting unlinks a note from the section; it stays in the master list. Follows the legacy <c>EditNoteUI</c>.
/// </summary>
/// <remarks>The open panel is chosen by the querystring, not held in the session as the legacy control did. The editor reads what the
/// tab's page loaded from the <paramref name="host"/> and reports back through its properties.</remarks>
/// <param name="host">The page whose tab the editor is on.</param>
/// <param name="definition">The kind of note the editor edits.</param>
/// <param name="list">The table's paging and sort choices, as the request gave them; the editor normalises them.</param>
public sealed class ProfileNoteEditor(ProfileNoteTabPageModel host, ProfileNoteEditorDefinition definition, ProfileNoteListState list)
{
    /// <summary>The <c>mode</c> querystring value that opens the "Add" panel.</summary>
    public const string AddMode = "add";

    /// <summary>The <c>mode</c> querystring value that opens the "Select" panel.</summary>
    public const string SelectMode = "select";

    /// <summary>The <c>result</c> querystring value that follows a save.</summary>
    public const string SavedNotice = "saved";

    /// <summary>The <c>result</c> querystring value that follows a delete.</summary>
    public const string DeletedNotice = "deleted";

    /// <summary>The sort column value that sorts the table by the note text.</summary>
    public const string ReferenceSortColumn = "Reference";

    /// <summary>The sort column value that sorts the table by the question reference.</summary>
    public const string QuestionReferenceSortColumn = "QuestionReference";

    /// <summary>The ascending sort direction.</summary>
    public const string AscendingDirection = "Ascending";

    /// <summary>The descending sort direction.</summary>
    public const string DescendingDirection = "Descending";

    /// <summary>The id of the Add panel's text box, which its error message links to.</summary>
    public const string NoteTextFieldId = "Input_NoteText";

    /// <summary>The id of the Select panel's drop-down, which its error message links to.</summary>
    public const string NoteSelectFieldId = "NoteId";

    /// <summary>The id of the first question checkbox, which the "no question ticked" error message links to.</summary>
    public const string FirstQuestionFieldId = "Input_QuestionIds_0";

    /// <summary>What the legacy control said when no note was chosen in the Select panel (<c>rfvNote</c>).</summary>
    public const string SelectNoteMessage = "Please select a reference";

    /// <summary>What the legacy control said when the Add panel's text was empty (<c>rfvNote</c>).</summary>
    public const string EnterNoteMessage = "Please enter a reference";

    /// <summary>What the legacy control said when no question was ticked.</summary>
    public const string SelectQuestionMessage = "At least one question must be selected";

    private const string ViewMode = "view";

    private ProfileNoteTypeDto? noteType;
    private bool notesLoaded;
    private IReadOnlyList<ProfileNoteDto> sectionNotes = [];
    private IReadOnlyList<ProfileNoteDto> availableNotes = [];
    private IReadOnlyList<ProfileQuestionInfoDto>? questions;
    private ProfileNoteDto? panelNote;

    /// <summary>Gets the fields posted by the open panel.</summary>
    public ProfileNoteInput Input { get; private set; } = new();

    /// <summary>Gets what names this editor in the querystring and in the names of its table's fields: empty when the tab has only this one.</summary>
    public string Key => definition.Key;

    /// <summary>Gets a prefix for the ids of the table's controls, so that the tab's tables do not share them.</summary>
    public string IdPrefix => Key.Length == 0 ? string.Empty : $"{Key}-";

    /// <summary>Gets a value indicating whether the tab has other editors, which then need a heading and a <c>type</c> to tell them apart.</summary>
    public bool HasSiblings => host.Editors.Count > 1;

    /// <summary>Gets the current 1-based results page.</summary>
    public int PageNumber => list.PageNumber;

    /// <summary>Gets the number of notes shown per page.</summary>
    public string PageSize => list.PageSize;

    /// <summary>Gets which column the table is sorted by.</summary>
    public string SortColumn => list.SortColumn;

    /// <summary>Gets the sort direction.</summary>
    public string SortDirection => list.SortDirection;

    /// <summary>Gets the singular name of the note type, as CDC.Api names it (for example "Scientific paper reference").</summary>
    public string NoteTypeName { get; private set; } = definition.FallbackName;

    /// <summary>Gets the plural name of the note type (for example "Scientific paper references").</summary>
    public string NoteTypePluralName { get; private set; } = definition.FallbackPluralName;

    /// <summary>Gets the label of the "Add" button (legacy <c>lblAddNote</c>).</summary>
    public string AddLabel => $"Add a new {NoteTypeName.ToLowerInvariant()}:";

    /// <summary>Gets the label of the "Select" button (legacy <c>lblSelectNote</c>).</summary>
    public string SelectLabel => $"Select a {NoteTypeName.ToLowerInvariant()} from the master list:";

    /// <summary>Gets the message shown when the section has no notes of the type (legacy <c>EmptyDataText</c>).</summary>
    public string NoNotesMessage => ProfileNoteDisplayHelper.BuildEmptyMessage(NoteTypePluralName);

    /// <summary>Gets the confirmation shown before a note is unlinked from the section.</summary>
    public string DeleteConfirmation => $"Are you sure you want to delete the association to this {NoteTypeName.ToLowerInvariant()}?";

    /// <summary>Gets the open panel's heading.</summary>
    public string PanelHeading => Panel switch
    {
        ProfileNotePanel.Add => $"Add a {NoteTypeName.ToLowerInvariant()}",
        ProfileNotePanel.Select => $"Select a {NoteTypeName.ToLowerInvariant()}",
        ProfileNotePanel.View => $"View a {NoteTypeName.ToLowerInvariant()}",
        _ => string.Empty
    };

    /// <summary>Gets the current page of the section's notes.</summary>
    public IReadOnlyList<ProfileNoteGridRow> Rows { get; private set; } = [];

    /// <summary>Gets the total number of the section's notes of the type, before paging.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the user may add, select, view and delete notes (legacy <c>NoteList.CanEditNoteList()</c>).</summary>
    public bool CanEdit => host.CanEditProfileSection && notesLoaded && noteType is not null;

    /// <summary>Gets which panel is open beneath the table.</summary>
    public ProfileNotePanel Panel { get; private set; }

    /// <summary>Gets the master-list notes that are not yet linked to this section, for the Select panel's drop-down.</summary>
    public IReadOnlyList<ProfileNoteChoice> AvailableNoteChoices =>
        [.. availableNotes.Select(note => new ProfileNoteChoice(note.Id, ProfileNoteDisplayHelper.ToSlimText(note.NoteText), note.Id == Input.NoteId))];

    /// <summary>Gets the full text of the note chosen in the Select panel, or shown in the View panel, without paragraph tags;
    /// <see langword="null"/> when there is none.</summary>
    public string? PanelNoteHtml => panelNote is null ? null : ProfileNoteDisplayHelper.StripParagraphTags(panelNote.NoteText);

    /// <summary>Gets the section's questions, each ticked when the note is linked to it.</summary>
    public IReadOnlyList<QuestionReferenceChoice> QuestionChoices { get; private set; } = [];

    /// <summary>Gets the validation messages of the open panel.</summary>
    public IReadOnlyList<ProfileNoteFormError> FormErrors { get; private set; } = [];

    /// <summary>Gets the result message shown above the tables, if any.</summary>
    public string? ActionMessage { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ActionMessage"/> reports a failure.</summary>
    public bool ActionMessageIsError { get; private set; }

    /// <summary>Gets a value indicating whether the editor has anything to report as a problem: a failed load or change, or a rejected panel.</summary>
    public bool HasFormErrors => FormErrors.Count > 0 || ActionMessageIsError;

    /// <summary>Gets the name of one of the table's fields as the request carries it: the plain name, or prefixed with the editor's key.</summary>
    /// <param name="name">The name when there is no prefix, for example <c>pageNumber</c>.</param>
    /// <returns>The name to put in the URL or the form.</returns>
    public string FieldName(string name) => Key.Length == 0 ? name : $"{Key}.{name}";

    /// <summary>Reads the section's notes and, for a user who may edit, the version's master list, resiliently: a failure degrades to
    /// an empty table, with editing switched off, and a message, rather than failing the page.</summary>
    /// <param name="noteTypesRequest">The read of CDC.Api's note types, shared by the tab's editors.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the notes have been read.</returns>
    public async Task LoadAsync(Task<IReadOnlyList<ProfileNoteTypeDto>> noteTypesRequest, CancellationToken cancellationToken)
    {
        try
        {
            var noteTypes = await noteTypesRequest;

            noteType = noteTypes.FirstOrDefault(type => ProfileNoteDisplayHelper.NormaliseNoteTypeName(type.Name) == definition.NormalisedName);

            if (noteType is null)
            {
                return;
            }

            NoteTypeName = noteType.Name;
            NoteTypePluralName = string.IsNullOrEmpty(noteType.PluralName) ? definition.FallbackPluralName : noteType.PluralName;

            // The section's notes and the version's master list do not depend on each other, so they are read at once.
            var sectionNotesRequest = host.ProfileSectionsApi.GetProfileNotesBySectionAsync(host.CurrentProfileVersionId, host.CurrentSection!.Id, noteType.Id, cancellationToken);
            var versionNotesRequest = host.CanEditProfileSection
                ? host.ProfileSectionsApi.GetProfileNotesByVersionAsync(host.CurrentProfileVersionId, noteType.Id, cancellationToken)
                : Task.FromResult<IReadOnlyList<ProfileNoteDto>>([]);

            await Task.WhenAll(sectionNotesRequest, versionNotesRequest);

            sectionNotes = await sectionNotesRequest;

            if (host.CanEditProfileSection)
            {
                var versionNotes = await versionNotesRequest;
                var linkedIds = sectionNotes.Select(note => note.Id).ToHashSet();
                availableNotes = [.. versionNotes.Where(note => !linkedIds.Contains(note.Id))];
            }

            notesLoaded = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            host.Logger.ProfileNotesLoadFailed(exception, host.ProfileId);
            sectionNotes = [];
            availableNotes = [];
            SetFailure($"We could not load the {NoteTypePluralName.ToLowerInvariant()}. Try again later.");
        }
    }

    /// <summary>Shows the result of the previous change, which the redirect that followed it put in the querystring. A message the
    /// page already has (a failed load) is kept.</summary>
    /// <param name="notice">The <c>result</c> value.</param>
    public void ApplyNotice(string? notice)
    {
        if (ActionMessage is not null)
        {
            return;
        }

        if (string.Equals(notice, SavedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = $"{NoteTypeName} save succeeded";
        }
        else if (string.Equals(notice, DeletedNotice, StringComparison.OrdinalIgnoreCase))
        {
            ActionMessage = $"{NoteTypeName} deletion succeeded";
        }
    }

    /// <summary>Sorts and pages the section's notes into <see cref="Rows"/>, matching the legacy grid's default of the plain
    /// text ascending, and keeps the table's choices to the ones it offers.</summary>
    public void BuildRows()
    {
        var section = host.CurrentSection!;

        var allRows = sectionNotes
            .Select(note => new ProfileNoteGridRow(
                note.Id,
                note.NoteText,
                ProfileTitleHtmlFormatter.ToPlainText(note.NoteText),
                ProfileNoteDisplayHelper.BuildQuestionReferenceText(note, section),
                ProfileNoteDisplayHelper.ToSlimText(note.NoteText),
                Convert.ToBase64String(note.LastUpdated)))
            .ToList();

        var sortByQuestionReference = string.Equals(list.SortColumn, QuestionReferenceSortColumn, StringComparison.OrdinalIgnoreCase);
        var descending = string.Equals(list.SortDirection, DescendingDirection, StringComparison.OrdinalIgnoreCase);

        list.SortColumn = sortByQuestionReference ? QuestionReferenceSortColumn : ReferenceSortColumn;
        list.SortDirection = descending ? DescendingDirection : AscendingDirection;

        Func<ProfileNoteGridRow, string> sortKey = sortByQuestionReference ? row => row.QuestionReferenceDisplay : row => row.PlainText;
        var sorted = descending
            ? allRows.OrderByDescending(sortKey, StringComparer.OrdinalIgnoreCase)
            : allRows.OrderBy(sortKey, StringComparer.OrdinalIgnoreCase);

        var pageSize = ResolvePageSize();

        TotalRecords = allRows.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)pageSize));
        list.PageNumber = Math.Clamp(list.PageNumber, 1, TotalPages);

        Rows = [.. sorted.Skip((list.PageNumber - 1) * pageSize).Take(pageSize)];
    }

    /// <summary>Opens the panel the querystring asks for, when the user may edit and it applies.</summary>
    /// <param name="mode">The <c>mode</c> value: <c>add</c> or <c>select</c>.</param>
    /// <param name="noteId">The <c>noteId</c> value: the note chosen in the Select panel, or the one viewed.</param>
    /// <param name="requestInput">The tab's fields, which the panel fills in.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the panel is ready.</returns>
    public async Task OpenRequestedPanelAsync(string? mode, Guid? noteId, ProfileNoteInput requestInput, CancellationToken cancellationToken)
    {
        Input = requestInput;

        if (!CanEdit)
        {
            return;
        }

        if (string.Equals(mode, AddMode, StringComparison.OrdinalIgnoreCase))
        {
            await ShowPanelAsync(ProfileNotePanel.Add, cancellationToken);
        }
        else if (string.Equals(mode, SelectMode, StringComparison.OrdinalIgnoreCase))
        {
            // Offered to anyone who may edit, even when the master list has nothing left to select: the legacy page showed the
            // button again after Add was cancelled whatever the list held, and the drop-down then simply had no notes.
            Input.NoteId = noteId;
            await ShowPanelAsync(ProfileNotePanel.Select, cancellationToken);
        }
        else if (sectionNotes.FirstOrDefault(note => note.Id == noteId) is { } viewed)
        {
            // Legacy opened the note with the questions it is already linked to ticked.
            Input.NoteId = viewed.Id;
            Input.QuestionIds = [.. viewed.QuestionReferences.Where(reference => reference.ProfileSectionId == host.CurrentSection!.Id).Select(reference => reference.ProfileQuestionId)];
            await ShowPanelAsync(ProfileNotePanel.View, cancellationToken);
        }
    }

    /// <summary>Handles the panel's "OK" button: adds the new note, or links the selected or viewed one to the ticked questions.</summary>
    /// <param name="requestInput">The fields the panel posted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the request finished.</returns>
    public async Task<ProfileNoteOutcome> SaveAsync(ProfileNoteInput requestInput, CancellationToken cancellationToken)
    {
        Input = requestInput;

        var panel = ParsePanel(Input.Mode);

        if (panel == ProfileNotePanel.None)
        {
            return ProfileNoteOutcome.BadRequest;
        }

        if (!CanEdit)
        {
            SetFailureUnlessAlreadyReported($"{NoteTypeName} save failed: You cannot change a {NoteTypeName.ToLowerInvariant()} on this version.");

            return ProfileNoteOutcome.Redisplay;
        }

        if (!await ShowPanelAsync(panel, cancellationToken))
        {
            return ProfileNoteOutcome.Redisplay;
        }

        if (panel == ProfileNotePanel.View && panelNote is null)
        {
            Panel = ProfileNotePanel.None;
            SetFailure($"{NoteTypeName} save failed: The {NoteTypeName.ToLowerInvariant()} no longer exists.");

            return ProfileNoteOutcome.Redisplay;
        }

        NormaliseInput(panel);
        FormErrors = Validate(panel);

        if (FormErrors.Count > 0)
        {
            return ProfileNoteOutcome.Redisplay;
        }

        var result = await SaveChangeAsync(panel, cancellationToken);

        if (result.Outcome == ProfileNoteChangeOutcome.Success)
        {
            return ProfileNoteOutcome.Saved;
        }

        SetFailure($"{NoteTypeName} save failed: {result.ErrorMessage}", result.Outcome);

        return ProfileNoteOutcome.Redisplay;
    }

    /// <summary>Handles a row's "Delete" confirmation: unlinks the note from every question in the section. The note stays in the
    /// master list, so it can be selected again.</summary>
    /// <param name="noteId">The note to unlink.</param>
    /// <param name="lastUpdated">The note's Base64 row version, read when the table was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the request finished.</returns>
    public async Task<ProfileNoteOutcome> DeleteAsync(Guid noteId, string? lastUpdated, CancellationToken cancellationToken)
    {
        var lowerName = NoteTypeName.ToLowerInvariant();

        if (!CanEdit)
        {
            SetFailureUnlessAlreadyReported($"{NoteTypeName} deletion failed: You cannot change a {lowerName} on this version.");

            return ProfileNoteOutcome.Redisplay;
        }

        var note = sectionNotes.FirstOrDefault(candidate => candidate.Id == noteId);
        var rowVersion = ProfileNoteChangeBuilder.DecodeRowVersion(lastUpdated);

        if (note is null || rowVersion is null)
        {
            SetFailure(note is null
                ? $"{NoteTypeName} deletion failed: The {lowerName} no longer exists."
                : $"{NoteTypeName} deletion failed: The {lowerName} could not be identified. Reload the page and try again.");

            return ProfileNoteOutcome.Redisplay;
        }

        var update = ProfileNoteChangeBuilder.BuildRemoval(note, rowVersion, host.CurrentSection!.Id);
        var result = update is null
            ? new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Success }
            : await ChangeAsync([], [update], cancellationToken);

        if (result.Outcome == ProfileNoteChangeOutcome.Success)
        {
            return ProfileNoteOutcome.Deleted;
        }

        SetFailure($"{NoteTypeName} deletion failed: {result.ErrorMessage}", result.Outcome);

        return ProfileNoteOutcome.Redisplay;
    }

    /// <summary>Handles the panel's "Tick all" and "Untick all" buttons: ticks or unticks every question, keeping everything else entered.</summary>
    /// <param name="tickAll"><see langword="true"/> to tick every question, <see langword="false"/> to untick them.</param>
    /// <param name="requestInput">The fields the panel posted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the request finished.</returns>
    public async Task<ProfileNoteOutcome> RedisplayPanelAsync(bool tickAll, ProfileNoteInput requestInput, CancellationToken cancellationToken)
    {
        Input = requestInput;

        var panel = ParsePanel(Input.Mode);

        if (panel == ProfileNotePanel.None)
        {
            return ProfileNoteOutcome.BadRequest;
        }

        if (!CanEdit)
        {
            return ProfileNoteOutcome.Redisplay;
        }

        var sectionQuestions = await LoadQuestionsAsync(cancellationToken);

        if (sectionQuestions is null)
        {
            return ProfileNoteOutcome.Redisplay;
        }

        Input.QuestionIds = tickAll ? [.. sectionQuestions.Select(question => question.Id)] : [];
        await ShowPanelAsync(panel, cancellationToken);

        return ProfileNoteOutcome.Redisplay;
    }

    /// <summary>Builds the URL of the tab with no panel open, optionally on another results page of this table.</summary>
    /// <param name="page">The results page, or <see langword="null"/> for the current one.</param>
    /// <returns>The page URL.</returns>
    public string? BuildListUrl(int? page = null) => BuildUrl(null, null, page ?? PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens the "Add" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildAddUrl() => BuildUrl(AddMode, null, PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens the "Select" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildSelectUrl() => BuildUrl(SelectMode, null, PageNumber, SortColumn, SortDirection);

    /// <summary>Builds the URL that opens a note's "View" panel.</summary>
    /// <param name="noteId">The note.</param>
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

    /// <summary>Lists the hidden fields a form on the tab carries so that every table keeps its paging and sort, and the section stays.</summary>
    /// <param name="exceptOwnPaging"><see langword="true"/> to leave out this table's page and page size, for the form that chooses them.</param>
    /// <returns>The field names and values.</returns>
    public IEnumerable<KeyValuePair<string, string>> StateFields(bool exceptOwnPaging = false) =>
        host.BuildStateFields(exceptOwnPaging ? this : null);

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
        host.BuildNoteUrl(this, mode, noteId, new ProfileNoteListState { PageNumber = page, PageSize = PageSize, SortColumn = sortColumn, SortDirection = sortDirection });

    /// <summary>Resolves the page size to one of the offered options, and keeps the choice to match.</summary>
    private int ResolvePageSize()
    {
        if (!NotesModel.PageSizeOptions.Contains(list.PageSize))
        {
            list.PageSize = NotesModel.PageSizeOptions[0];
        }

        return int.Parse(list.PageSize, CultureInfo.InvariantCulture);
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
                    $"{host.CurrentSection!.SectionNumber}.{question.QuestionNumber}",
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
            questions = await host.ProfileSectionsApi.GetProfileQuestionInfoListAsync(host.CurrentSection!.Id, cancellationToken);

            return questions;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            host.Logger.ProfileSectionQuestionsLoadFailed(exception, host.ProfileId, host.CurrentSection!.Id);
            SetFailure("We could not load the questions for this section. Try again later.");

            return null;
        }
    }

    /// <summary>Sanitises a new note's text, which comes from a link-only rich text editor and so is untrusted. The other panels post none.</summary>
    private void NormaliseInput(ProfileNotePanel panel) =>
        Input.NoteText = panel == ProfileNotePanel.Add ? ReviewCommentHtml.Sanitize(Input.NoteText) : string.Empty;

    /// <summary>Applies the legacy panel's rules: a new note needs text, a selected one must be chosen, and at least one question
    /// must be ticked.</summary>
    private List<ProfileNoteFormError> Validate(ProfileNotePanel panel)
    {
        var errors = new List<ProfileNoteFormError>();

        if (panel == ProfileNotePanel.Add && ProfileTitleHtmlFormatter.ToPlainText(Input.NoteText).Length == 0)
        {
            errors.Add(new ProfileNoteFormError(NoteTextFieldId, EnterNoteMessage));
        }
        else if (panel == ProfileNotePanel.Select && panelNote is null)
        {
            errors.Add(new ProfileNoteFormError(NoteSelectFieldId, SelectNoteMessage));
        }

        if (Input.QuestionIds.Count == 0)
        {
            errors.Add(new ProfileNoteFormError(FirstQuestionFieldId, SelectQuestionMessage));
        }

        return errors;
    }

    private async Task<ProfileNoteChangeResult> SaveChangeAsync(ProfileNotePanel panel, CancellationToken cancellationToken)
    {
        var sectionId = host.CurrentSection!.Id;

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
            return await host.ProfileSectionsApi.UpdateProfileNotesAsync(host.CurrentProfileVersionId, noteType!.Id, inserts, updates, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            host.Logger.ProfileNoteChangeFailed(exception, host.ProfileId);

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
            host.Logger.ProfileNoteChangeRefused(host.ProfileId, outcome.Value);
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
}
