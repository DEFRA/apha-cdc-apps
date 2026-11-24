using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages;

/// <summary>The privacy notice linked from the footer, open to signed-out visitors as well.</summary>
[AllowAnonymous]
public class PrivacyNoticeModel : PageModel
{
}
