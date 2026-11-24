using System.Net;
using CDC.Web.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace CDC.Web.Tests.Infrastructure;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task APdf_GetsEveryHeaderButTheContentSecurityPolicy()
    {
        await using var app = await StartAsync(context =>
        {
            context.Response.ContentType = "application/pdf";
            return context.Response.Body.WriteAsync("%PDF-"u8.ToArray()).AsTask();
        });

        var response = await app.GetTestClient().GetAsync("/report");

        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", string.Join(",", response.Headers.GetValues("X-Frame-Options")));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    // The exception handler clears the response before it renders the error page; the headers are written as the response
    // starts, so they survive that.
    [Fact]
    public async Task HeadersSurviveTheResponseBeingCleared()
    {
        await using var app = await StartAsync(context =>
        {
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        var response = await app.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("DENY", string.Join(",", response.Headers.GetValues("X-Frame-Options")));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    // Only the origin of each sign-in address is allowed, and anything that is not an absolute https:// address is left out.
    [Fact]
    public async Task FormAction_TakesTheOriginOfEachHttpsSignInAddress()
    {
        await using var app = await StartAsync(
            _ => Task.CompletedTask,
            signInAddresses: ["https://idp.test/idphub/b2c", "https://IDP.test/other", "http://insecure.test", "", "not a url"]);

        var policy = string.Join(",", (await app.GetTestClient().GetAsync("/")).Headers.GetValues("Content-Security-Policy"));

        Assert.Contains("form-action 'self' https://idp.test;", policy, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", "connect-src 'self' ws://localhost:* wss://localhost:*;")]
    [InlineData("Production", "connect-src 'self';")]
    public async Task OnlyDevelopment_MayConnectToTheLocalHotReloadServer(string environment, string expected)
    {
        await using var app = await StartAsync(_ => Task.CompletedTask, environment: environment);

        var policy = string.Join(",", (await app.GetTestClient().GetAsync("/")).Headers.GetValues("Content-Security-Policy"));

        Assert.Contains(expected, policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageSeesTheSameNonceAsThePolicy()
    {
        string? pageNonce = null;
        await using var app = await StartAsync(context =>
        {
            pageNonce = context.GetCspNonce();
            return Task.CompletedTask;
        });

        var policy = string.Join(",", (await app.GetTestClient().GetAsync("/")).Headers.GetValues("Content-Security-Policy"));

        Assert.False(string.IsNullOrEmpty(pageNonce));
        Assert.Contains($"script-src 'self' 'nonce-{pageNonce}';", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCspNonce_IsEmptyWhenTheMiddlewareDidNotRun()
    {
        Assert.Equal(string.Empty, new DefaultHttpContext().GetCspNonce());
    }

    private static async Task<WebApplication> StartAsync(
        RequestDelegate endpoint,
        string environment = "Production",
        string[]? signInAddresses = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseSecurityHeaders(signInAddresses ?? ["https://idp.test"]);
        app.Run(endpoint);
        await app.StartAsync();

        return app;
    }
}
