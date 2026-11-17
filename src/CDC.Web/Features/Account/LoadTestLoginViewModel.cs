namespace CDC.Web.Features.Account;

/// <summary>The Load test login page: which user to sign in as, for the journey chosen on the landing page.</summary>
public sealed class LoadTestLoginViewModel
{
    /// <summary>Gets or sets the journey chosen on the landing page: <c>internal</c> or <c>external</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the local page to return to after signing in, when a page that needs a signed-in user sent the user here.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>Gets or sets the email address of the user to sign in as.</summary>
    public string? EmailAddress { get; set; }
}
