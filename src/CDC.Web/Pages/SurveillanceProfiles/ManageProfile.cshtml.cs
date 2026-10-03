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
