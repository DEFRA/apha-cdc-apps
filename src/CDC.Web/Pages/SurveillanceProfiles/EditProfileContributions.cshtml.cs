using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Contributions tab of a profile section: who has contributed to it and when. Replaces the legacy
/// <c>EditProfileContributions.aspx</c>. Shown only to users allowed to see it (legacy
/// <c>ProfileContributionInfoList.CanGetProfileContributionInfoList()</c>).
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileContributionsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileContributionsModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.Contributions;

    /// <summary>Gets the selected section's logged contributions.</summary>
    public IReadOnlyList<ProfileSectionContributionView> Contributions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
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

        if (!CanViewContributions)
        {
            return NotFound();
        }

        await LoadContributionsAsync(cancellationToken);

        return Page();
    }

    /// <summary>Loads the contributions, resiliently: a failure degrades to an empty table rather than failing the page.</summary>
    private async Task LoadContributionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var contributions = await ProfileSectionsApi.GetProfileVersionSectionContributionsAsync(CurrentProfileVersionId, CurrentSection!.Id, cancellationToken);

            Contributions =
            [
                .. contributions.Select(contribution => new ProfileSectionContributionView(
                    contribution.FullName,
                    contribution.UserName,
                    contribution.OrganisationName,
                    contribution.LastContributionDate.ToString(DateDisplayFormat, CultureInfo.InvariantCulture)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileSectionContributionsLoadFailed(exception, ProfileId);
        }
    }
}
