using System.Globalization;
using System.Security.Claims;
using CDC.Web.Features.Account;
using CDC.Web.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The legacy "Application error" page, which showed the technical details of the error that sent the user to it. A profile page
/// sends the user here, through <see cref="RedirectFrom"/>, when CDC.Api refuses them profile data they may not read: the legacy
/// site threw a <c>SecurityException</c> there and its error handler showed this page. The details travel in TempData, as the
/// legacy site kept them in the session, so they are shown once and are gone when the page is refreshed.
/// </summary>
/// <param name="userDisplayNames">Names the signed-in user.</param>
/// <param name="timeProvider">Supplies the time of the error; defaults to the system clock.</param>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ApplicationErrorModel(IUserDisplayNameService userDisplayNames, TimeProvider? timeProvider = null)
    : BreadcrumbPageModelBase(BrowseProfileName)
{
    /// <summary>The path of this page.</summary>
    public const string PagePath = "/SurveillanceProfiles/ApplicationError";

    // The legacy page kept the breadcrumb trail of the page the user had asked for: Search disease profiles > Browse profile.
    private const string BrowseProfileName = "Browse profile";
    private const string SearchPagePath = "/SurveillanceProfiles/Search";

    private const string ErrorTypeKey = "ApplicationError.ErrorType";
    private const string ErrorLocationKey = "ApplicationError.ErrorLocation";
    private const string DetailsKey = "ApplicationError.Details";

    // The legacy page printed the time with the server's culture, which showed as "10/28/2026 7:41:37 PM".
    private const string TimeOfErrorFormat = "M/d/yyyy h:mm:ss tt";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Gets the signed-in user, by name and email address.</summary>
    public string? UserDescription { get; private set; }

    /// <summary>Gets the type of the error, as the legacy page showed the type of the exception.</summary>
    public string? ErrorType { get; private set; }

    /// <summary>Gets the address of the page that hit the error.</summary>
    public string? ErrorLocation { get; private set; }

    /// <summary>Gets when the error was shown.</summary>
    public string? TimeOfError { get; private set; }

    /// <summary>Gets what went wrong, as the legacy page showed the message of the exception.</summary>
    public string? Details { get; private set; }

    /// <summary>Gets a value indicating whether a page sent the user here with details to show; they are not kept for a refresh.</summary>
    public bool HasTechnicalDetails => Details is not null;

    /// <summary>Sends the user to this page, leaving the details it shows in TempData.</summary>
    /// <param name="page">The page that hit the error; its request is the error's location.</param>
    /// <param name="errorType">The type of the error.</param>
    /// <param name="details">What went wrong.</param>
    /// <returns>The redirect to this page.</returns>
    public static IActionResult RedirectFrom(PageModel page, string errorType, string details)
    {
        ArgumentNullException.ThrowIfNull(page);

        page.TempData[ErrorTypeKey] = errorType;
        page.TempData[ErrorLocationKey] = page.Request.GetDisplayUrl();
        page.TempData[DetailsKey] = details;

        return page.RedirectToPage(PagePath);
    }

    public void OnGet()
    {
        Breadcrumb = new BreadcrumbViewModel(BrowseProfileName, "Search disease profiles", Url.Page(SearchPagePath));
        Details = TempData[DetailsKey] as string;

        if (Details is null)
        {
            return;
        }

        ErrorType = TempData[ErrorTypeKey] as string;
        ErrorLocation = TempData[ErrorLocationKey] as string;
        UserDescription = DescribeUser();
        TimeOfError = clock.GetLocalNow().ToString(TimeOfErrorFormat, CultureInfo.InvariantCulture);
    }

    private string DescribeUser()
    {
        var name = userDisplayNames.GetDisplayName(User);
        var email = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? User.FindFirst("preferred_username")?.Value;

        return string.IsNullOrWhiteSpace(email) || string.Equals(email, name, StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name} ({email})";
    }
}
