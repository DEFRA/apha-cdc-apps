using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CDC.Web.Tests.TestSupport;

// Minimal IUrlHelper so controller unit tests can exercise Url.Action/Url.IsLocalUrl without a real routing context.
internal sealed class FakeUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();

    // Falls back to "Account" when no controller is specified (mirrors ASP.NET Core's real ambient
    // RouteData resolution for same-controller Url.Action(action) calls, e.g. AccountController's
    // own Url.Action(nameof(SignedOut))).
    public string? Action(UrlActionContext actionContext) => $"/{actionContext.Controller ?? "Account"}/{actionContext.Action}";

    public string? Content(string? contentPath) => contentPath;

    public bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);

    public string? Link(string? routeName, object? values) => throw new NotImplementedException();

    public string? RouteUrl(UrlRouteContext routeContext) => throw new NotImplementedException();
}
