using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The References tab of a profile section: its scientific paper and legislative references. Replaces the legacy
/// <c>EditProfileReferences.aspx</c>.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileReferencesModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileReferencesModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.References;

    /// <summary>Gets the selected section's scientific paper references.</summary>
    public ProfileNoteGroupView ScientificPaperReferences { get; private set; } = ProfileNoteDisplayHelper.EmptyGroup("Scientific paper references");

    /// <summary>Gets the selected section's legislative references.</summary>
    public ProfileNoteGroupView LegislativeReferences { get; private set; } = ProfileNoteDisplayHelper.EmptyGroup("Legislative references");

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (IsReady)
        {
            await LoadReferencesAsync(cancellationToken);
        }

        return Page();
    }

    /// <summary>Loads the notes, resiliently: a failure degrades to empty lists rather than failing the page.</summary>
    private async Task LoadReferencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var noteTypes = await ProfileSectionsApi.GetProfileNoteTypesAsync(cancellationToken);
            var questionNumbersById = ProfileNoteDisplayHelper.BuildQuestionNumbersById(Sections);
            var section = new ProfileNoteSectionContext(CurrentProfileVersionId, CurrentSection!.Id, questionNumbersById);

            ScientificPaperReferences = await ProfileNoteDisplayHelper.LoadGroupAsync(
                ProfileSectionsApi, noteTypes, ProfileNoteDisplayHelper.ScientificPaperReferenceType, "Scientific paper references",
                section, cancellationToken);

            LegislativeReferences = await ProfileNoteDisplayHelper.LoadGroupAsync(
                ProfileSectionsApi, noteTypes, ProfileNoteDisplayHelper.LegislativeReferenceType, "Legislative references",
                section, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileNotesLoadFailed(exception, ProfileId);
        }
    }
}
