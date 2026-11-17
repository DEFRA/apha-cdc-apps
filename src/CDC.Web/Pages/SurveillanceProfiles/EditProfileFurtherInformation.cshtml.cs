using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Further information tab of a profile section: its sources of further information. A source is added, picked from the
/// version's master list, viewed (to change which of the section's questions it is linked to) or deleted (unlinked from the
/// section; it stays in the master list). Replaces the legacy <c>EditProfileFurtherInformation.aspx</c> and the
/// <c>EditNoteUI</c> control it hosts; the work is done by the page's <see cref="ProfileNoteEditor"/>.
/// </summary>
/// <remarks>
/// The page is addressed by the profile and the section; the profile's current version, which the legacy page took from its
/// <c>ProfileVersionId</c> querystring value, is resolved from the profile.
/// </remarks>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileFurtherInformationModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileFurtherInformationModel> logger) : ProfileNoteTabPageModel(apiClient, profileSectionsApiService, logger)
{
    private readonly ProfileNoteListState list = new();

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.FurtherInformation;

    /// <inheritdoc />
    public override string PanelId => "profile-further-information-panel";

    /// <inheritdoc />
    public override int ListPageNumber => PageNumber;

    /// <inheritdoc />
    public override int ListTotalPages => Editor.TotalPages;

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber
    {
        get => list.PageNumber;
        set => list.PageNumber = value;
    }

    /// <summary>Gets or sets the number of notes shown per page.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize
    {
        get => list.PageSize;
        set => list.PageSize = value;
    }

    /// <summary>Gets or sets which column the table is sorted by: <see cref="ProfileNoteEditor.ReferenceSortColumn"/> or <see cref="ProfileNoteEditor.QuestionReferenceSortColumn"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string SortColumn
    {
        get => list.SortColumn;
        set => list.SortColumn = value;
    }

    /// <summary>Gets or sets the sort direction: "Ascending" or "Descending".</summary>
    [BindProperty(SupportsGet = true)]
    public string SortDirection
    {
        get => list.SortDirection;
        set => list.SortDirection = value;
    }

    /// <summary>Gets the editor of the page's sources of further information.</summary>
    public ProfileNoteEditor Editor => Editors[0];

    /// <inheritdoc />
    protected override IReadOnlyList<ProfileNoteEditor> CreateEditors() =>
        [new ProfileNoteEditor(this, ProfileNoteEditorDefinition.SourceOfFurtherInformation, list)];
}
