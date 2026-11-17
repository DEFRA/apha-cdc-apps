using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The All Revision dates tab: every section's author, VA and policy review frequency, next review date and status,
/// with a "Set For All Sections" area. Replaces the legacy <c>EditProfileAllRevisionDates.aspx</c>. Shown only to users
/// allowed to see it (legacy <c>ProfileVersionSection.CanGetRevisionInformation()</c>) and editable only to a profile
/// editor, or to an author for the one review their role owns. Below the tab are the "Publish (public)" and "Publish (Defranet only)"
/// links, shown to whoever may publish the profile.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileAllRevisionDatesModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileAllRevisionDatesModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    private const string SaveFailedPrefix = "Profile section save failed: ";
    private const string PublishFailedMessage = "We could not publish the profile. Try again later.";

    private ProfileVersionAllRevisionDatesDto? allRevisionDatesSource;

    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.AllRevisionDates;

    /// <inheritdoc />
    public override bool HasFormErrors =>
        RevisionDatesErrors.Count > 0 || RevisionDatesSaveError is not null || InvalidReasons.Count > 0 || PublishErrorMessage is not null;

    /// <summary>Gets what the tab renders (every section's revision data and the drop-down options), or
    /// <see langword="null"/> when it could not be loaded.</summary>
    public RevisionDatesTabView? RevisionDatesTab { get; private set; }

    /// <summary>Gets the validation messages from the most recent Save. Empty when it succeeded or has not been
    /// used; when non-empty, nothing was sent to CDC.Api.</summary>
    public IReadOnlyList<string> RevisionDatesErrors { get; private set; } = [];

    /// <summary>Gets the number of sections written by the most recent Save, or <see langword="null"/> until it has succeeded.</summary>
    public int? RevisionDatesSavedCount { get; private set; }

    /// <summary>Gets the failure message from the most recent Save, if any.</summary>
    public string? RevisionDatesSaveError { get; private set; }

    /// <summary>Gets the validation errors that stopped the draft being published, in the order the legacy <c>InvalidReasonList</c>
    /// listed them; empty unless a publish was refused because the draft is invalid.</summary>
    public IReadOnlyList<ProfileSectionInvalidReasonDto> InvalidReasons { get; private set; } = [];

    /// <summary>Gets the message for a publish that failed for any other reason (the legacy <c>lblErrorDesc</c>), if any.</summary>
    public string? PublishErrorMessage { get; private set; }

    /// <summary>Gets or sets the message the Profile reports page shows after a publish, which reads the same TempData key.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets whether the Profile reports page generates every report of the new version when it loads (the legacy
    /// <c>PublishFlag</c>), which reads the same TempData key.</summary>
    [TempData]
    public bool GenerateReports { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var early = await LoadTabContextAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        await LoadAllRevisionDatesAsync(cancellationToken);

        return Page();
    }

    /// <summary>Publishes the profile and makes it public (legacy <c>lnkPublishPublic</c>).</summary>
    public Task<IActionResult> OnPostPublishPublicAsync(CancellationToken cancellationToken) =>
        PublishAsync(isPublic: true, "Successfully published this profile and made it public.", cancellationToken);

    /// <summary>Publishes the profile for DefraNet only (legacy <c>lnkPublish</c>).</summary>
    public Task<IActionResult> OnPostPublishDefranetOnlyAsync(CancellationToken cancellationToken) =>
        PublishAsync(isPublic: false, "Successfully published this profile but did not make it public.", cancellationToken);

    /// <summary>
    /// Handles the Save buttons, matching the legacy <c>PersistSectionInfoToDatabase()</c>. Every section's submitted
    /// frequencies, next review dates and statuses are validated, then written by CDC.Api in one transaction. Invalid
    /// input is reported back, keeping what the user typed, without calling the save endpoint.
    /// </summary>
    /// <param name="profileVersionId">The profile version being edited, posted from a hidden field.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, showing the saved values, the validation errors or the failure message.</returns>
    public async Task<IActionResult> OnPostSaveRevisionDatesAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await OnGetAsync(cancellationToken);

        if (result is NotFoundResult || RevisionDatesTab is null || allRevisionDatesSource is null)
        {
            return result;
        }

        if (!CanEditAnyReviewDates)
        {
            RevisionDatesSaveError = "You do not have permission to edit the revision dates of this profile version.";
            return Page();
        }

        var parsed = ProfileRevisionDatesForm.Parse(Request.Form, allRevisionDatesSource, EditableReviewDates);
        var submittedTab = RevisionDatesTab with
        {
            Rows = parsed.Rows,
            SetForAll = ProfileRevisionDatesForm.ReadSetForAll(Request.Form)
        };

        if (!parsed.IsValid)
        {
            RevisionDatesTab = submittedTab;
            RevisionDatesErrors = parsed.Errors;
            return Page();
        }

        try
        {
            var saveResult = await ProfileSectionsApi.SaveProfileVersionRevisionDatesAsync(
                profileVersionId, parsed.Inputs, cancellationToken);

            if (saveResult.Outcome == SaveRevisionDatesOutcome.Success)
            {
                RevisionDatesSavedCount = saveResult.SectionsSavedCount;

                // Re-load so the tab shows the saved values and the new row versions.
                await LoadAllRevisionDatesAsync(cancellationToken);
            }
            else
            {
                RevisionDatesSaveError = SaveFailedPrefix + saveResult.ErrorMessage;

                if (saveResult.Outcome == SaveRevisionDatesOutcome.Conflict)
                {
                    Logger.ProfileRevisionDatesSaveConflict(ProfileId);

                    // The submitted row versions are stale, so show the latest stored values instead.
                    await LoadAllRevisionDatesAsync(cancellationToken);
                }
                else
                {
                    RevisionDatesTab = submittedTab;
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileRevisionDatesSaveFailed(exception, ProfileId);
            RevisionDatesTab = submittedTab;
            RevisionDatesSaveError = SaveFailedPrefix + "We could not save the revision dates. Try again later.";
        }

        return Page();
    }

    /// <summary>Loads the profile, its flags and the sections, and decides whether the tab can be shown at all.</summary>
    /// <returns>The result to return instead of showing the tab, or <see langword="null"/> when it can be shown.</returns>
    private async Task<IActionResult?> LoadTabContextAsync(CancellationToken cancellationToken)
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

        return CanViewAllRevisionDates ? null : NotFound();
    }

    /// <summary>Mirrors legacy <c>lnkPublish_Click</c>: an invalid draft is listed here and not published; otherwise the new version's Profile reports page opens.</summary>
    private async Task<IActionResult> PublishAsync(bool isPublic, string successMessage, CancellationToken cancellationToken)
    {
        var early = await LoadTabContextAsync(cancellationToken);

        if (early is not null)
        {
            return early;
        }

        try
        {
            var published = await ApiClient.PublishProfileAsync(ProfileId, isPublic, cancellationToken);

            switch (published.Outcome)
            {
                case PublishProfileOutcome.Success:
                    Logger.ProfilePublished(ProfileId, isPublic);
                    SuccessMessage = successMessage;
                    GenerateReports = true;

                    return RedirectToPage("/SurveillanceProfiles/ProfileReports", new { profileVersionId = published.NewProfileVersionId });

                case PublishProfileOutcome.InvalidProfile:
                    Logger.ProfileNotPublishedBecauseInvalid(ProfileId, isPublic, published.InvalidReasons.Count);
                    InvalidReasons = published.InvalidReasons;
                    break;

                default:
                    Logger.ProfilePublishRefused(ProfileId, isPublic, published.Outcome);
                    PublishErrorMessage = published.ErrorMessage ?? PublishFailedMessage;
                    break;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfilePublishFailed(exception, ProfileId);
            PublishErrorMessage = PublishFailedMessage;
        }

        await LoadAllRevisionDatesAsync(cancellationToken);

        return Page();
    }

    /// <summary>Loads the revision dates, resiliently: a failure degrades to an empty tab rather than failing the page.</summary>
    private async Task LoadAllRevisionDatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var revisionDates = await ProfileSectionsApi.GetProfileVersionAllRevisionDatesAsync(CurrentProfileVersionId, cancellationToken);

            allRevisionDatesSource = revisionDates;
            RevisionDatesTab = new RevisionDatesTabView(
                ProfileRevisionDatesForm.BuildRows(revisionDates),
                ProfileRevisionDatesForm.BuildOptions(revisionDates.ReviewFrequencies),
                ProfileRevisionDatesForm.BuildOptions(revisionDates.ReviewStatuses),
                new RevisionDatesSetForAllView(),
                CanEditAnyReviewDates)
            {
                Editable = EditableReviewDates
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileAllRevisionDatesLoadFailed(exception, ProfileId);
        }
    }

    /// <summary>Gets the reviews the user may change: all three for a profile editor, the one their role owns for an author.</summary>
    private EditableReviews EditableReviewDates => new(CanEditAuthorReviewDates, CanEditVaReviewDates, CanEditPolicyReviewDates);
}
