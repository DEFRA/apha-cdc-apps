using CDC.Web.Features.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages;

/// <summary>The cookies page linked from the footer, open to signed-out visitors as well.</summary>
[AllowAnonymous]
public class CookiesModel(IOptions<SessionTimeoutOptions> sessionTimeout) : PageModel
{
    /// <summary>Gets how many minutes a signed-in session may sit idle before the user has to sign in again.</summary>
    public int IdleTimeoutMinutes { get; } = (int)sessionTimeout.Value.IdleTimeout.TotalMinutes;
}
