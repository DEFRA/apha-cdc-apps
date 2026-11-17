using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// A tab of a profile section that edits notes (References, Further information). Each kind of note on the tab has a
/// <see cref="ProfileNoteEditor"/>, as the legacy page had an <c>EditNoteUI</c> for each; this page binds the request, finds the editor
/// it is about and turns what the editor did into a response.
/// </summary>
/// <remarks>Only one panel is open at a time, the one named by <see cref="NoteTypeKey"/> (<c>type</c>) with <see cref="Mode"/> or
/// <see cref="NoteId"/>. Each editor's table keeps its own page and sort; a tab with one editor needs no <c>type</c> and no prefix.</remarks>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public abstract class ProfileNoteTabPageModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    private IReadOnlyList<ProfileNoteEditor>? editors;

    /// <summary>Gets or sets the open panel (<c>add</c> or <c>select</c>); with only a <see cref="NoteId"/> the View panel opens.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    /// <summary>Gets or sets the note the panel is about, bound from the querystring: the one chosen in the Select panel, or the one viewed.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? NoteId { get; set; }

    /// <summary>Gets or sets which editor a request is about (<c>type</c>), when the tab has several. Forms carry it back.</summary>
    [BindProperty(Name = "type", SupportsGet = true)]
    public string? NoteTypeKey { get; set; }

    /// <summary>Gets or sets the result of the previous change (<c>saved</c> or <c>deleted</c>), set by the redirect that follows it.</summary>
    [BindProperty(Name = "result", SupportsGet = true)]
    public string? Notice { get; set; }

    /// <summary>Gets or sets the fields posted by the panel.</summary>
    [BindProperty]
    public ProfileNoteInput Input { get; set; } = new();

    /// <summary>Gets the id of the region the tab's pagination links reload in place.</summary>
    public abstract string PanelId { get; }

    /// <summary>Gets the tab's editors, in the order the tab shows them.</summary>
    public IReadOnlyList<ProfileNoteEditor> Editors => editors ??= CreateEditors();

    /// <inheritdoc />
    public override bool HasFormErrors => Editors.Any(editor => editor.HasFormErrors);

    /// <summary>Gets the editor the request is about: the tab's only one, or the one named by <see cref="NoteTypeKey"/>.</summary>
    protected ProfileNoteEditor? ActiveEditor => Editors.Count == 1
        ? Editors[0]
        : Editors.FirstOrDefault(editor => string.Equals(editor.Key, NoteTypeKey, StringComparison.OrdinalIgnoreCase));

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        if (ActiveEditor is { } active)
        {
            active.ApplyNotice(Notice);
            await active.OpenRequestedPanelAsync(Mode, NoteId, Input, cancellationToken);
        }

        return Page();
    }

    /// <summary>Handles the panel's "OK" button: adds the new note, or links the selected or viewed one to the ticked questions.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the table after a change, or the page with the panel open when the submission was not accepted.</returns>
    public Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) =>
        RunAsync(editor => editor.SaveAsync(Input, cancellationToken), cancellationToken);

    /// <summary>Handles a row's "Delete" confirmation: unlinks the note from every question in the section. The note stays in the
    /// master list, so it can be selected again.</summary>
    /// <param name="noteId">The note to unlink.</param>
    /// <param name="lastUpdated">The note's Base64 row version, read when the table was shown.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the table after the change, or the page with the reason it failed.</returns>
    public Task<IActionResult> OnPostDeleteAsync(Guid noteId, string? lastUpdated, CancellationToken cancellationToken) =>
        RunAsync(editor => editor.DeleteAsync(noteId, lastUpdated, cancellationToken), cancellationToken);

    /// <summary>Handles the panel's "Tick all" button: ticks every question, keeping everything else entered.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page with the panel open.</returns>
    public Task<IActionResult> OnPostTickAllAsync(CancellationToken cancellationToken) =>
        RunAsync(editor => editor.RedisplayPanelAsync(tickAll: true, Input, cancellationToken), cancellationToken);

    /// <summary>Handles the panel's "Untick all" button: unticks every question, keeping everything else entered.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page with the panel open.</returns>
    public Task<IActionResult> OnPostUntickAllAsync(CancellationToken cancellationToken) =>
        RunAsync(editor => editor.RedisplayPanelAsync(tickAll: false, Input, cancellationToken), cancellationToken);

    /// <summary>Creates the tab's editors, once the request's paging and sort choices are bound.</summary>
    /// <returns>The editors, in the order the tab shows them.</returns>
    protected abstract IReadOnlyList<ProfileNoteEditor> CreateEditors();

    /// <summary>Builds the URL of the tab as one editor's link needs it: with the paging and sort of every table, the target's
    /// as given, and the panel the link opens.</summary>
    /// <param name="target">The editor the link belongs to.</param>
    /// <param name="mode">The panel to open (<c>add</c> or <c>select</c>), or <see langword="null"/>.</param>
    /// <param name="noteId">The note to open, or <see langword="null"/>.</param>
    /// <param name="targetList">The target's paging and sort choices the link leads to.</param>
    /// <returns>The page URL.</returns>
    internal string? BuildNoteUrl(ProfileNoteEditor target, string? mode, Guid? noteId, ProfileNoteListState targetList)
    {
        var values = new RouteValueDictionary
        {
            ["profileId"] = ProfileId,
            ["section"] = CurrentSection?.Id,
            ["mode"] = mode,
            ["noteId"] = noteId
        };

        if (Editors.Count > 1 && (mode is not null || noteId is not null))
        {
            values["type"] = target.Key;
        }

        AddListValues(values, target, targetList);

        return Url.Page(ProfileSectionTabs.PagePath(ActiveTab), values);
    }

    /// <summary>Lists the hidden fields a form carries so that the tab's tables keep their paging and sort, and the section stays.</summary>
    /// <param name="exceptPagingOf">The editor whose page and page size are left out, for the form that chooses them, or <see langword="null"/>.</param>
    /// <returns>The field names and values.</returns>
    internal IEnumerable<KeyValuePair<string, string>> BuildStateFields(ProfileNoteEditor? exceptPagingOf)
    {
        yield return new("section", CurrentSection!.Id.ToString());

        foreach (var editor in Editors)
        {
            if (editor != exceptPagingOf)
            {
                yield return new(editor.FieldName("pageNumber"), editor.PageNumber.ToString(CultureInfo.InvariantCulture));
                yield return new(editor.FieldName("pageSize"), editor.PageSize);
            }

            yield return new(editor.FieldName("sortColumn"), editor.SortColumn);
            yield return new(editor.FieldName("sortDirection"), editor.SortDirection);
        }
    }

    private static async Task<IReadOnlyList<ProfileNoteTypeDto>> ReadNoteTypesAsync(IProfileSectionsApiService api, CancellationToken cancellationToken) =>
        await api.GetProfileNoteTypesAsync(cancellationToken);

    private void AddListValues(RouteValueDictionary values, ProfileNoteEditor? target, ProfileNoteListState? targetList)
    {
        foreach (var editor in Editors)
        {
            var list = editor == target ? targetList : null;

            values[editor.FieldName("pageNumber")] = list?.PageNumber ?? editor.PageNumber;
            values[editor.FieldName("pageSize")] = list?.PageSize ?? editor.PageSize;
            values[editor.FieldName("sortColumn")] = list?.SortColumn ?? editor.SortColumn;
            values[editor.FieldName("sortDirection")] = list?.SortDirection ?? editor.SortDirection;
        }
    }

    /// <summary>Loads the profile, then every editor's notes at once, so that the page can be shown or a change checked.</summary>
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

        var noteTypes = ReadNoteTypesAsync(ProfileSectionsApi, cancellationToken);

        await Task.WhenAll(Editors.Select(editor => editor.LoadAsync(noteTypes, cancellationToken)));

        foreach (var editor in Editors)
        {
            editor.BuildRows();
        }

        return null;
    }

    /// <summary>Loads the page and has the editor the request is about act on it.</summary>
    private async Task<IActionResult> RunAsync(Func<ProfileNoteEditor, Task<ProfileNoteOutcome>> action, CancellationToken cancellationToken)
    {
        var early = await InitialiseAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        if (ActiveEditor is not { } editor)
        {
            return BadRequest();
        }

        var outcome = await action(editor);

        return outcome switch
        {
            ProfileNoteOutcome.Saved => RedirectAfterChange(editor, ProfileNoteEditor.SavedNotice),
            ProfileNoteOutcome.Deleted => RedirectAfterChange(editor, ProfileNoteEditor.DeletedNotice),
            ProfileNoteOutcome.BadRequest => BadRequest(),
            _ => Page()
        };
    }

    /// <summary>Redirects after a successful change (Post-Redirect-Get), so a refresh cannot repeat it. The paging and sort of every table are kept.</summary>
    private RedirectToPageResult RedirectAfterChange(ProfileNoteEditor editor, string notice)
    {
        var values = new RouteValueDictionary { ["profileId"] = ProfileId, ["section"] = CurrentSection!.Id };

        if (Editors.Count > 1)
        {
            values["type"] = editor.Key;
        }

        AddListValues(values, null, null);
        values["result"] = notice;

        return RedirectToPage(ProfileSectionTabs.PagePath(ActiveTab), values);
    }
}
