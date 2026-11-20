namespace CDC.Web.Infrastructure.Security;

public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds the browser security headers (Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
    /// and Permissions-Policy) to every response. Register it before UseStaticFiles so static files get them too.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="signInAddresses">The identity providers' addresses, which forms may be redirected to.</param>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, params string[] signInAddresses) =>
        // Passed as one collection: a bare string[] would be spread into UseMiddleware's own params object[] arguments.
        app.UseMiddleware<SecurityHeadersMiddleware>((IReadOnlyCollection<string>)signInAddresses);

    /// <summary>
    /// This response's Content-Security-Policy nonce, for the <c>nonce</c> attribute of an inline script. Empty when
    /// <see cref="UseSecurityHeaders"/> did not run, in which case the script is not covered by a policy either.
    /// </summary>
    public static string GetCspNonce(this HttpContext context) =>
        context.Items[SecurityHeadersMiddleware.NonceItemKey] as string ?? string.Empty;
}
