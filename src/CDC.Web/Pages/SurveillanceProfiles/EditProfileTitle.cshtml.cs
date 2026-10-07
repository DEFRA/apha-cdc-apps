using System.ComponentModel.DataAnnotations;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Razor Page for editing a profile's title. Replaces the legacy <c>EditProfileTitle.aspx</c>.
/// </summary>
public class EditProfileTitleModel : PageModel
{
    private static readonly Action<ILogger, Guid, Exception?> LogProfileNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogProfileNotFoundMessage)),
            "Profile '{ProfileId}' was not found when editing its title");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadProfileMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToLoadProfileMessage)),
            "Failed to load profile '{ProfileId}' attributes");
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailedToSaveProfileTitleMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(3, nameof(LogFailedToSaveProfileTitleMessage)),
            "Failed to save profile title for profile '{ProfileId}': {ErrorMessage}");
    private static readonly Action<ILogger, Guid, Exception?> LogSavedProfileTitleMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogSavedProfileTitleMessage)),
            "Profile title updated for profile '{ProfileId}'");

    private readonly IApiClient apiClient;
    private readonly ILogger<EditProfileTitleModel> logger;

    public EditProfileTitleModel(IApiClient apiClient, ILogger<EditProfileTitleModel> logger)
    {
        this.apiClient = apiClient;
        this.logger = logger;
    }

    /// <summary>Gets or sets the profile whose title is being edited, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the editable profile title.</summary>
    [BindProperty]
    [Required(ErrorMessage = "You must enter a profile title")]
    [StringLength(255, ErrorMessage = "Profile title cannot exceed 255 characters")]
    public string ProfileTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the base64-encoded row version read alongside the profile, so a
    /// concurrent edit can be detected when saving.</summary>
    [BindProperty]
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>Gets the success message to display after a save, if any.</summary>
    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var status = await LoadProfileAsync(cancellationToken);

        return status switch
        {
            ProfileLoadStatus.NotFound => NotFound(),
            ProfileLoadStatus.Error => Page(),
            _ => Page()
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        byte[] lastUpdatedBytes;
        try
        {
            lastUpdatedBytes = Convert.FromBase64String(LastUpdated);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The page has expired. Reload the profile and try again.");
            return Page();
        }

        var result = await apiClient.UpdateProfileTitleAsync(ProfileId, ProfileTitle.Trim(), lastUpdatedBytes, cancellationToken);

        if (result.Outcome != UpdateProfileTitleOutcome.Success)
        {
            LogFailedToSaveProfileTitleMessage(logger, ProfileId, result.ErrorMessage ?? string.Empty, null);
            ModelState.AddModelError(string.Empty, $"Profile save failed: {result.ErrorMessage}");
            return Page();
        }

        LogSavedProfileTitleMessage(logger, ProfileId, null);
        StatusMessage = "The profile title was successfully updated";

        await LoadProfileAsync(cancellationToken);
        return Page();
    }

    private async Task<ProfileLoadStatus> LoadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var attributes = await apiClient.GetProfileAttributesAsync(ProfileId, cancellationToken);

            if (attributes is null)
            {
                LogProfileNotFoundMessage(logger, ProfileId, null);
                return ProfileLoadStatus.NotFound;
            }

            ProfileTitle = attributes.Title;
            LastUpdated = Convert.ToBase64String(attributes.LastUpdated);

            return ProfileLoadStatus.Success;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadProfileMessage(logger, ProfileId, exception);
            ModelState.AddModelError(string.Empty, "Unable to load the profile. Please try again.");

            return ProfileLoadStatus.Error;
        }
    }

    private enum ProfileLoadStatus
    {
        Success,
        NotFound,
        Error
    }
}
