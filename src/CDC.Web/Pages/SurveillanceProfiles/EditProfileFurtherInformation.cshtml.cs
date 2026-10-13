using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Further information tab of a profile section: its sources of further information. Replaces the legacy
/// <c>EditProfileFurtherInformation.aspx</c>.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileFurtherInformationModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileFurtherInformationModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.FurtherInformation;

    /// <summary>Gets the selected section's sources of further information.</summary>
    public ProfileNoteGroupView FurtherInformation { get; private set; } = ProfileNoteDisplayHelper.EmptyGroup("Sources of further information");

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (IsReady)
        {
            await LoadFurtherInformationAsync(cancellationToken);
        }

        return Page();
    }

    /// <summary>Loads the notes, resiliently: a failure degrades to an empty list rather than failing the page.</summary>
    private async Task LoadFurtherInformationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var noteTypes = await ProfileSectionsApi.GetProfileNoteTypesAsync(cancellationToken);
            var questionNumbersById = ProfileNoteDisplayHelper.BuildQuestionNumbersById(Sections);
            var section = new ProfileNoteSectionContext(CurrentProfileVersionId, CurrentSection!.Id, questionNumbersById);

            FurtherInformation = await ProfileNoteDisplayHelper.LoadGroupAsync(
                ProfileSectionsApi, noteTypes, ProfileNoteDisplayHelper.SourceOfFurtherInformationType, "Sources of further information",
                section, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileNotesLoadFailed(exception, ProfileId);
        }
    }
}
