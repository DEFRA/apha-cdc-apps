namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The paging and sort choices of one table of notes. A tab that shows several tables binds one of these for each (from the
/// querystring, and from the forms that carry it), so that paging or sorting one table leaves the others as they were.
/// </summary>
public sealed class ProfileNoteListState
{
    /// <summary>Gets or sets the current 1-based results page.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the number of notes shown per page.</summary>
    public string PageSize { get; set; } = NotesModel.PageSizeOptions[0];

    /// <summary>Gets or sets which column the table is sorted by: <see cref="ProfileNoteEditor.ReferenceSortColumn"/> or <see cref="ProfileNoteEditor.QuestionReferenceSortColumn"/>.</summary>
    public string SortColumn { get; set; } = ProfileNoteEditor.ReferenceSortColumn;

    /// <summary>Gets or sets the sort direction: "Ascending" or "Descending".</summary>
    public string SortDirection { get; set; } = ProfileNoteEditor.AscendingDirection;
}
