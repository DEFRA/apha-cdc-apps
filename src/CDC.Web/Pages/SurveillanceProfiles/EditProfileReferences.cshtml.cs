using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The References tab of a profile section: its scientific paper and legislative references. Each kind is added, picked from the
/// version's master list, viewed (to change which of the section's questions it is linked to) or deleted (unlinked from the
/// section; it stays in the master list), as on the legacy <c>EditProfileReferences.aspx</c>, which hosted an <c>EditNoteUI</c>
/// control for each. The work is done by the page's two <see cref="ProfileNoteEditor"/>s.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileReferencesModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileReferencesModel> logger) : ProfileNoteTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.References;

    /// <inheritdoc />
    public override string PanelId => "profile-references-panel";

    /// <summary>Gets or sets the scientific paper references table's page and sort, bound from the querystring under <c>scientific</c>.</summary>
    [BindProperty(Name = ProfileNoteEditorDefinition.ScientificPaperKey, SupportsGet = true)]
    public ProfileNoteListState ScientificPaperList { get; set; } = new();

    /// <summary>Gets or sets the legislative references table's page and sort, bound from the querystring under <c>legislative</c>.</summary>
    [BindProperty(Name = ProfileNoteEditorDefinition.LegislativeKey, SupportsGet = true)]
    public ProfileNoteListState LegislativeList { get; set; } = new();

    /// <summary>Gets the editor of the section's scientific paper references.</summary>
    public ProfileNoteEditor ScientificPaperReferences => Editors[0];

    /// <summary>Gets the editor of the section's legislative references.</summary>
    public ProfileNoteEditor LegislativeReferences => Editors[1];

    /// <inheritdoc />
    protected override IReadOnlyList<ProfileNoteEditor> CreateEditors() =>
    [
        new ProfileNoteEditor(this, ProfileNoteEditorDefinition.ScientificPaperReference, ScientificPaperList),
        new ProfileNoteEditor(this, ProfileNoteEditorDefinition.LegislativeReference, LegislativeList)
    ];
}
