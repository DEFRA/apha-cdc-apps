using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.TestSupport;

// An IAntiforgery for controller tests, which run without the antiforgery services: every request is valid unless told otherwise.
internal sealed class FakeAntiforgery : IAntiforgery
{
    public const string RequestToken = "request-token";
    public const string HeaderName = "RequestVerificationToken";

    public bool RequestIsValid { get; init; } = true;

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new(RequestToken, "cookie-token", "__RequestVerificationToken", HeaderName);

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(RequestIsValid);

    public Task ValidateRequestAsync(HttpContext httpContext) => throw new NotSupportedException();

    public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();
}
