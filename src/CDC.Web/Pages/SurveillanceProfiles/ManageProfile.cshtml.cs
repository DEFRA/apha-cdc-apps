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

    /// <summary>Gets the profile's "Manage profile" details, once loaded.</summary>
    public ManageProfileViewModel? Profile { get; private set; }

    /// <summary>Gets a value indicating whether the profile failed to load.</summary>
    public bool HasError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (Profile is null)
            {
                LogProfileNotFoundMessage(logger, ProfileId, null);
                return NotFound();
            }

            return Page();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadProfileMessage(logger, ProfileId, exception);
            HasError = true;

            return Page();
        }
    }
}
