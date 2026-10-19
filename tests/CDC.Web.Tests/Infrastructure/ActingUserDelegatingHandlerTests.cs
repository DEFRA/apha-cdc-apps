using System.Net;
using System.Security.Claims;
using CDC.Common.Auth;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.Infrastructure;

public class ActingUserDelegatingHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("88d1e0c6-7d10-427d-a82f-33fc2c1cef28");

    [Fact]
    public async Task SendAsync_SendsTheSignedInUsersIdInTheActingUserHeader()
    {
        var request = await SendAsync(SignedIn(UserId.ToString()));

        Assert.Equal(UserId.ToString(), Assert.Single(request.Headers.GetValues(ActingUserHeader.Name)));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task SendAsync_SendsNoHeader_WhenTheClaimIsNotARealUserId(string claimValue)
    {
        var request = await SendAsync(SignedIn(claimValue));

        Assert.False(request.Headers.Contains(ActingUserHeader.Name));
    }

    [Fact]
    public async Task SendAsync_SendsNoHeader_WhenTheSignedInUserHasNoUserRecord()
    {
        var request = await SendAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "limited")], "test")));

        Assert.False(request.Headers.Contains(ActingUserHeader.Name));
    }

    [Fact]
    public async Task SendAsync_SendsNoHeader_WhenNobodyIsSignedIn()
    {
        var request = await SendAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(request.Headers.Contains(ActingUserHeader.Name));
    }

    [Fact]
    public async Task SendAsync_SendsNoHeader_OutsideARequest()
    {
        var request = await SendAsync(user: null);

        Assert.False(request.Headers.Contains(ActingUserHeader.Name));
    }

    [Fact]
    public async Task SendAsync_ReplacesAHeaderAlreadyOnTheRequest_WithTheSignedInUsersId()
    {
        var request = await SendAsync(SignedIn(UserId.ToString()), "someone-else");

        Assert.Equal(UserId.ToString(), Assert.Single(request.Headers.GetValues(ActingUserHeader.Name)));
    }

    [Fact]
    public async Task SendAsync_DropsAHeaderAlreadyOnTheRequest_WhenNobodyIsSignedIn()
    {
        var request = await SendAsync(new ClaimsPrincipal(new ClaimsIdentity()), Guid.NewGuid().ToString());

        Assert.False(request.Headers.Contains(ActingUserHeader.Name));
    }

    private static ClaimsPrincipal SignedIn(string internalUserId) =>
        new(new ClaimsIdentity([new Claim(ExternalUserClaimTypes.InternalUserId, internalUserId)], "test"));

    // A null user means there is no HttpContext at all, as for a call made outside a request.
    private static async Task<HttpRequestMessage> SendAsync(ClaimsPrincipal? user, string? existingHeader = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = user is null ? null : new DefaultHttpContext { User = user } };
        var handler = new ActingUserDelegatingHandler(accessor) { InnerHandler = new OkHandler() };
        var request = new HttpRequestMessage(HttpMethod.Get, "https://cdc-api.test/api/species");

        if (existingHeader is not null)
        {
            request.Headers.Add(ActingUserHeader.Name, existingHeader);
        }

        using var invoker = new HttpMessageInvoker(handler);
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        return request;
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
