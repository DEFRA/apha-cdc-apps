using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.TestSupport;

// Minimal no-op IAuthenticationService so controller unit tests can call SignInAsync/SignOutAsync
// without a full authentication pipeline. Records every SignOutAsync call (scheme + properties) so
// tests can assert both which schemes a controller action signed out of, and whether a RedirectUri
// was attached to each individual sign-out.
internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public List<(string? Scheme, AuthenticationProperties? Properties)> SignOutCalls { get; } = [];

    public IEnumerable<string?> SignOutSchemes => SignOutCalls.Select(call => call.Scheme);

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOutCalls.Add((scheme, properties));
        return Task.CompletedTask;
    }
}
