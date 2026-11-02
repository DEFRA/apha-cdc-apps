using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.TestSupport;

// Minimal no-op IAuthenticationService so controller unit tests can call SignInAsync/SignOutAsync
// without a full authentication pipeline. Records every SignOutAsync call (scheme + properties) so
// tests can assert both which schemes a controller action signed out of, and whether a RedirectUri
// was attached to each individual sign-out.
internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public List<(string? Scheme, AuthenticationProperties? Properties)> SignOutCalls { get; } = [];

    public IEnumerable<string?> SignOutSchemes => SignOutCalls.Select(call => call.Scheme);

    /// <summary>Creates an accessor to a request that signs in and out through a new fake, for code that reaches the request that way.</summary>
    public static (IHttpContextAccessor Accessor, FakeAuthenticationService AuthService) CreateAccessor()
    {
        var authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() } };

        return (accessor, authService);
    }

    public AuthenticateResult AuthenticateResultToReturn { get; set; } = AuthenticateResult.NoResult();

    /// <summary>Gets the result for particular schemes, which takes precedence over <see cref="AuthenticateResultToReturn"/>.</summary>
    public Dictionary<string, AuthenticateResult> AuthenticateResultsByScheme { get; } = [];

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(scheme is not null && AuthenticateResultsByScheme.TryGetValue(scheme, out var result) ? result : AuthenticateResultToReturn);

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
