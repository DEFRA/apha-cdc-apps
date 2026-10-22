using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Razor Page for editing a profile's title. Replaces the legacy <c>EditProfileTitle.aspx</c>.
/// Only users the legacy <c>Profile.CanEditProfile()</c> allowed - profile editors - may use it.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanEditProfile)]
public class EditProfileTitleModel : PageModel
{
    /// <summary>The message for a title with no text, which the page's script also shows as soon as the title is emptied.</summary>
    public const string TitleRequiredMessage = "You must enter a profile title";

    private const int MaxTitleLength = 255;

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
    private static readonly Action<ILogger, Guid, Exception?> LogWhatIfScenarioMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogWhatIfScenarioMessage)),
            "Profile '{ProfileId}' is a what-if scenario, so its profile title cannot be edited");

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

    /// <summary>Gets or sets the editable profile title, which may carry the editor's italic formatting.</summary>
    [BindProperty]
    public string? ProfileTitle { get; set; }

    /// <summary>Gets or sets the base64-encoded row version read alongside the profile, so a
    /// concurrent edit can be detected when saving.</summary>
    [BindProperty]
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>Gets the success message to display after a save, if any.</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>Gets a value indicating whether the form is shown. It is not for a "what-if" scenario, which has no profile title to edit.</summary>
    public bool ShowForm { get; private set; } = true;

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
        var title = (ProfileTitle ?? string.Empty).Trim();
        ValidateTitle(title);

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

        var result = await apiClient.UpdateProfileTitleAsync(ProfileId, title, lastUpdatedBytes, cancellationToken);

        if (result.Outcome != UpdateProfileTitleOutcome.Success)
        {
            LogFailedToSaveProfileTitleMessage(logger, ProfileId, result.ErrorMessage ?? string.Empty, null);

            // A title the API refuses (too long, already in use) belongs to the title field; any other failure is general, as the legacy page showed it.
            if (result.Outcome == UpdateProfileTitleOutcome.ValidationFailed)
            {
                ModelState.AddModelError(nameof(ProfileTitle), result.ErrorMessage ?? "The profile title could not be saved.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"Profile save failed: {result.ErrorMessage}");
            }

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

            if (attributes.IsWhatIfScenario)
            {
                LogWhatIfScenarioMessage(logger, ProfileId, null);
                ModelState.AddModelError(string.Empty, "You cannot edit the profile title of a \"what-if\" scenario.");
                ShowForm = false;

                return ProfileLoadStatus.Refused;
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

    // Counts the formatting markup too, as the legacy page did: the database stores the title with it.
    private void ValidateTitle(string title)
    {
        if (ProfileTitleHtmlFormatter.ToPlainText(title).Length == 0)
        {
            // Leaves the editor empty, not holding the <br> or &nbsp; it left behind.
            ProfileTitle = string.Empty;
            ModelState.AddModelError(nameof(ProfileTitle), TitleRequiredMessage);
        }
        else if (title.Length > MaxTitleLength)
        {
            ModelState.AddModelError(nameof(ProfileTitle), $"Profile title cannot exceed {MaxTitleLength} characters");
        }
    }

    private enum ProfileLoadStatus
    {
        Success,
        NotFound,
        Refused,
        Error
    }
}
