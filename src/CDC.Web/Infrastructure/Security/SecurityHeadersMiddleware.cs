using System.Buffers.Text;
using System.Security.Cryptography;

namespace CDC.Web.Infrastructure.Security;

/// <summary>
/// Adds the browser security headers to every response (CWE-1021, OWASP A05:2021 Security Misconfiguration), static files
/// and error pages included. The headers are written as the response starts, so a later middleware that clears the response
/// (the exception handler does, before it renders the error page) cannot drop them.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    internal const string NonceItemKey = "CDC.Web.Security.CspNonce";

    // 128 bits, as the CSP specification recommends for a nonce.
    private const int NonceSizeInBytes = 16;

    private readonly RequestDelegate _next;
    private readonly string _formAction;
    private readonly string _connectSource;

    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="environment">In Development the page may also connect to the local hot reload server.</param>
    /// <param name="signInOrigins">
    /// The identity providers' addresses (CIDM and Entra ID). A form posted after the session has ended is answered with a
    /// redirect to one of them, and form-action applies to that redirect as well as to the post itself.
    /// </param>
    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment, IReadOnlyCollection<string> signInOrigins)
    {
        _next = next;
        _formAction = string.Join(' ', OriginsOf(signInOrigins).Prepend("'self'"));
        _connectSource = environment.IsDevelopment() ? "'self' ws://localhost:* wss://localhost:*" : "'self'";
    }

    public Task InvokeAsync(HttpContext context)
    {
        // A new nonce for every request, so a script injected into a page cannot know it. The layout writes it on its
        // inline scripts (see SecurityHeadersExtensions.GetCspNonce). URL-safe base64, as Razor would HTML-encode a '+'.
        context.Items[NonceItemKey] = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(NonceSizeInBytes));
        context.Response.OnStarting(ApplyHeaders, context);

        return _next(context);
    }

    private Task ApplyHeaders(object state)
    {
        var context = (HttpContext)state;
        var headers = context.Response.Headers;

        // Stops the browser guessing ("sniffing") a response's type from its content, so for example an uploaded file
        // served as text/plain or application/pdf is never run as script or rendered as HTML.
        headers.XContentTypeOptions = "nosniff";

        // Stops any site, this one included, showing the page in a frame, which clickjacking relies on. Kept for older
        // browsers; frame-ancestors 'none' in the Content-Security-Policy below is the primary protection.
        headers.XFrameOptions = "DENY";

        // Sends the full address only to this site. Another site is told only the origin, and nothing at all over plain
        // HTTP, so identifiers in a page's address (profile and version ids) do not leak in the Referer header.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Turns off browser features the service never uses, for this page and any frame inside it, so a script that did
        // get in could not ask for the user's location, microphone or camera.
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // A PDF is not an HTML page, and a policy written for HTML pages can stop the browser's built-in PDF viewer
        // showing it. X-Frame-Options above still stops a PDF being framed.
        if (!IsPdf(context.Response.ContentType))
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy(context);
        }

        return Task.CompletedTask;
    }

    // Content-Security-Policy: what a page may load and run, and who may frame it. Everything the app uses (GOV.UK
    // Frontend's CSS, JavaScript, fonts and images, Bootstrap and jQuery) is served by the app itself.
    private string ContentSecurityPolicy(HttpContext context)
    {
        var nonce = context.Items[NonceItemKey] as string;

        var directives = new List<string>
        {
            // Anything not named below may come only from this site.
            "default-src 'self'",

            // This site's own script files, and only the inline scripts carrying this response's nonce: the layout's
            // import map, the GOV.UK Frontend "js-enabled" class and initAll(). Inline event handlers (onclick=...),
            // javascript: links and eval() are all refused, which is what stops most cross-site scripting.
            $"script-src 'self' 'nonce-{nonce}'",

            // This site's own stylesheets only; no <style> elements or style="..." attributes. Scripts can still set
            // element.style, which the policy does not restrict.
            "style-src 'self'",

            // data: is for the small SVG icons embedded in Bootstrap's stylesheet.
            "img-src 'self' data:",

            // GOV.UK Frontend's fonts are served from /assets/fonts.
            "font-src 'self'",

            // fetch() calls (paged lists, search results, the session timeout) go only to this site.
            $"connect-src {_connectSource}",

            // No Flash, Java or other plugins.
            "object-src 'none'",

            // A <base> element injected into a page cannot point its relative links and scripts at another site.
            "base-uri 'self'",

            // Forms may post only to this site, or end up at the identity providers when the session has ended.
            $"form-action {_formAction}",

            // No site, this one included, may show the page in a frame: the primary protection against clickjacking.
            "frame-ancestors 'none'"
        };

        // Behind the load balancer every page is served over HTTPS, so any http:// address a page holds is fetched over
        // HTTPS instead of being sent in the clear. Left out of plain-HTTP responses (a local run), where it would break
        // every asset.
        if (context.Request.IsHttps)
        {
            directives.Add("upgrade-insecure-requests");
        }

        return string.Join("; ", directives);
    }

    private static bool IsPdf(string? contentType) =>
        contentType is not null && contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> OriginsOf(IEnumerable<string> addresses) => addresses
        .Select(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.GetLeftPart(UriPartial.Authority)
            : null)
        .OfType<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
