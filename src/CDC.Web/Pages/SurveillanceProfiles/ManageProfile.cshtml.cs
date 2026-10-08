using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Razor Page for managing a profile. Replaces the legacy <c>ManageProfile.aspx</c>.
/// </summary>
public class ManageProfileModel : PageModel
{
    private static readonly Action<ILogger, Guid, Exception?> LogProfileNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogProfileNotFoundMessage)),
            "Profile '{ProfileId}' was not found when managing it");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadProfileMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToLoadProfileMessage)),
            "Failed to load profile '{ProfileId}' management details");
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailedToUpdateProfileStatusMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(3, nameof(LogFailedToUpdateProfileStatusMessage)),
            "Failed to update status for profile '{ProfileId}': {ErrorMessage}");
    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedProfileStatusMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogUpdatedProfileStatusMessage)),
            "Profile status updated for profile '{ProfileId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogCreatedNewDraftVersionMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(5, nameof(LogCreatedNewDraftVersionMessage)),
            "Created a new draft version for profile '{ProfileId}'");
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailedToCreateNewDraftVersionMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(6, nameof(LogFailedToCreateNewDraftVersionMessage)),
            "Failed to create a new draft version for profile '{ProfileId}': {ErrorMessage}");
    private static readonly Action<ILogger, Guid, bool, Exception?> LogDeletedProfileVersionMessage =
        LoggerMessage.Define<Guid, bool>(
            LogLevel.Information,
            new EventId(7, nameof(LogDeletedProfileVersionMessage)),
            "Deleted the current version for profile '{ProfileId}' (profile also deleted: {IsProfileDeleted})");
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailedToDeleteProfileVersionMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(8, nameof(LogFailedToDeleteProfileVersionMessage)),
            "Failed to delete the current version for profile '{ProfileId}': {ErrorMessage}");
    private static readonly Action<ILogger, Guid, bool, Exception?> LogPublishedProfileMessage =
        LoggerMessage.Define<Guid, bool>(
            LogLevel.Information,
            new EventId(9, nameof(LogPublishedProfileMessage)),
            "Published profile '{ProfileId}' as a new version (public: {IsPublic})");
    private static readonly Action<ILogger, Guid, bool, string, Exception?> LogFailedToPublishProfileMessage =
        LoggerMessage.Define<Guid, bool, string>(
            LogLevel.Error,
            new EventId(10, nameof(LogFailedToPublishProfileMessage)),
            "Failed to publish profile '{ProfileId}' (public: {IsPublic}): {ErrorMessage}");

    private readonly IApiClient apiClient;
    private readonly ILogger<ManageProfileModel> logger;

    public ManageProfileModel(IApiClient apiClient, ILogger<ManageProfileModel> logger)
    {
        this.apiClient = apiClient;
        this.logger = logger;
    }

    /// <summary>Gets or sets the profile being managed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the status dropdown's selection, bound on save.</summary>
    [BindProperty]
    public Guid ProfileStatusId { get; set; }

    /// <summary>Gets the profile's "Manage profile" details, once loaded.</summary>
    public ManageProfileViewModel? Profile { get; private set; }

    /// <summary>Gets every status the profile can be set to.</summary>
    public IReadOnlyList<ProfileStatusTypeDto> ProfileStatusTypes { get; private set; } = [];

    /// <summary>Gets a value indicating whether the profile failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the success message to display after a status save, if any.</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>Gets the error message to display after a failed status save, if any.</summary>
    public string? StatusErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadProfileAsync(cancellationToken);

        if (!loaded)
        {
            return HasError ? Page() : NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.UpdateProfileStatusAsync(ProfileId, ProfileStatusId, cancellationToken);

        if (result.Outcome == UpdateProfileStatusOutcome.Success)
        {
            LogUpdatedProfileStatusMessage(logger, ProfileId, null);
            StatusMessage = "Successfully updated the profile status.";
        }
        else
        {
            LogFailedToUpdateProfileStatusMessage(logger, ProfileId, result.ErrorMessage ?? string.Empty, null);
            StatusErrorMessage = result.ErrorMessage;
        }

        var loaded = await LoadProfileAsync(cancellationToken);

        return loaded || HasError ? Page() : NotFound();
    }

    /// <summary>
    /// Creates a new draft version from the profile's <c>LatestVersionId</c> and redirects to
    /// browse it, matching the legacy <c>lnkNewDraftVersion_Click</c> handler
    /// (<c>profileData.CreateNewDraft()</c> then <c>Response.Redirect("EditProfileQuestions.aspx...")</c>).
    /// On failure, redisplays Manage profile with an inline error, matching the legacy page.
    /// </summary>
    public async Task<IActionResult> OnPostCreateNewDraftVersionAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadProfileAsync(cancellationToken);

        if (!loaded)
        {
            return HasError ? Page() : NotFound();
        }

        var result = await apiClient.CreateNewProfileVersionAsync(
            Profile!.LatestVersionId, isPublished: false, isPublic: false, cancellationToken);

        if (result.Outcome == CreateNewProfileVersionOutcome.Success)
        {
            LogCreatedNewDraftVersionMessage(logger, ProfileId, null);
            return RedirectToPage("/SurveillanceProfiles/EditProfileQuestions", new { profileId = ProfileId });
        }

        LogFailedToCreateNewDraftVersionMessage(logger, ProfileId, result.ErrorMessage ?? string.Empty, null);
        StatusErrorMessage = result.ErrorMessage;

        return Page();
    }

    /// <summary>
    /// Deletes the profile's current draft version (<c>LatestVersionId</c>) and redirects to the
    /// home page, matching the legacy <c>lnkDelete_Click</c> handler (<c>profileData.DeleteLatestDraft()</c>
    /// then an unconditional <c>Response.Redirect("~/Home.aspx")</c> - regardless of whether the
    /// whole profile was also deleted as a result). On failure, redisplays Manage profile with an
    /// inline error, matching the legacy page.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadProfileAsync(cancellationToken);

        if (!loaded)
        {
            return HasError ? Page() : NotFound();
        }

        var result = await apiClient.DeleteProfileVersionAsync(Profile!.LatestVersionId, cancellationToken);

        if (result.Outcome == DeleteProfileVersionOutcome.Success)
        {
            LogDeletedProfileVersionMessage(logger, ProfileId, result.IsProfileDeleted, null);
            return RedirectToAction("Index", "Landing");
        }

        LogFailedToDeleteProfileVersionMessage(logger, ProfileId, result.ErrorMessage ?? string.Empty, null);
        StatusErrorMessage = result.ErrorMessage;

        return Page();
    }

    /// <summary>
    /// Publishes the profile's current draft version (<c>LatestVersionId</c>) as a new published
    /// version, matching the legacy <c>lnkPublish_Click</c> handler shared by <c>lnkPublishPublic</c>
    /// and <c>lnkPublish</c> (<c>profileData.Publish(makePublic)</c>). Redisplays Manage profile
    /// with a success banner afterwards - the legacy redirect target, <c>ProfileReports.aspx</c>,
    /// has no modern equivalent yet. On failure, redisplays Manage profile with an inline error,
    /// matching the legacy page.
    /// </summary>
    private async Task<IActionResult> PublishAsync(bool isPublic, string successMessage, CancellationToken cancellationToken)
    {
        var loaded = await LoadProfileAsync(cancellationToken);

        if (!loaded)
        {
            return HasError ? Page() : NotFound();
        }

        var result = await apiClient.CreateNewProfileVersionAsync(
            Profile!.LatestVersionId, isPublished: true, isPublic: isPublic, cancellationToken);

        if (result.Outcome == CreateNewProfileVersionOutcome.Success)
        {
            LogPublishedProfileMessage(logger, ProfileId, isPublic, null);
            StatusMessage = successMessage;
        }
        else
        {
            LogFailedToPublishProfileMessage(logger, ProfileId, isPublic, result.ErrorMessage ?? string.Empty, null);
            StatusErrorMessage = result.ErrorMessage;
        }

        loaded = await LoadProfileAsync(cancellationToken);

        return loaded || HasError ? Page() : NotFound();
    }

    /// <summary>Publishes the profile publicly - see <see cref="PublishAsync"/>.</summary>
    public Task<IActionResult> OnPostPublishPublicAsync(CancellationToken cancellationToken) =>
        PublishAsync(isPublic: true, "Successfully published this profile and made it public.", cancellationToken);

    /// <summary>Publishes the profile for DefraNet only (not public) - see <see cref="PublishAsync"/>.</summary>
    public Task<IActionResult> OnPostPublishDefranetOnlyAsync(CancellationToken cancellationToken) =>
        PublishAsync(isPublic: false, "Successfully published this profile but did not make it public.", cancellationToken);

    private async Task<bool> LoadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            Profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (Profile is null)
            {
                LogProfileNotFoundMessage(logger, ProfileId, null);
                return false;
            }

            ProfileStatusTypes = await apiClient.GetProfileStatusTypesAsync(cancellationToken);
            ProfileStatusId = Profile.ProfileStatusId;

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadProfileMessage(logger, ProfileId, exception);
            HasError = true;

            return false;
        }
    }
}
