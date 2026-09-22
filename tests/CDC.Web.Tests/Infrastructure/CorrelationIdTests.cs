using System.Net;
using System.Reflection;
using CDC.Common.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Infrastructure;

public class CorrelationIdTests
{
    [Fact]
    public async Task UseCorrelationId_UsesIncomingGuidAndEchoesItOnResponse()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var appBuilder = new ApplicationBuilder(serviceProvider);
        appBuilder.UseCorrelationId();
        appBuilder.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var app = appBuilder.Build();
        var requestId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddlewareExtensions.HeaderName] = requestId.ToString();

        await app.Invoke(context);

        Assert.Equal(requestId.ToString(), context.Items[CorrelationIdMiddlewareExtensions.HeaderName]);
        Assert.Equal(requestId.ToString(), context.Response.Headers[CorrelationIdMiddlewareExtensions.HeaderName].ToString());
    }

    [Fact]
    public async Task UseCorrelationId_GeneratesNewGuid_WhenHeaderIsMissingOrInvalid()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var appBuilder = new ApplicationBuilder(serviceProvider);
        appBuilder.UseCorrelationId();
        appBuilder.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var app = appBuilder.Build();

        var contextWithMissingHeader = new DefaultHttpContext();
        await app.Invoke(contextWithMissingHeader);

        Assert.True(Guid.TryParse(contextWithMissingHeader.Items[CorrelationIdMiddlewareExtensions.HeaderName]?.ToString(), out _));
        Assert.True(Guid.TryParse(contextWithMissingHeader.Response.Headers[CorrelationIdMiddlewareExtensions.HeaderName].ToString(), out _));

        var contextWithInvalidHeader = new DefaultHttpContext();
        contextWithInvalidHeader.Request.Headers[CorrelationIdMiddlewareExtensions.HeaderName] = "not-a-guid";

        await app.Invoke(contextWithInvalidHeader);

        Assert.True(Guid.TryParse(contextWithInvalidHeader.Items[CorrelationIdMiddlewareExtensions.HeaderName]?.ToString(), out _));
        Assert.True(Guid.TryParse(contextWithInvalidHeader.Response.Headers[CorrelationIdMiddlewareExtensions.HeaderName].ToString(), out _));
    }

    [Fact]
    public async Task SendAsync_AddsCorrelationIdHeader_WhenAvailableOnHttpContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        var correlationId = Guid.NewGuid();
        accessor.HttpContext.Items[CorrelationIdMiddlewareExtensions.HeaderName] = correlationId.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var response = await InvokeDelegatingHandlerAsync(accessor, request);

        Assert.Equal(correlationId.ToString(), response.RequestMessage!.Headers.GetValues(CorrelationIdMiddlewareExtensions.HeaderName).Single());
    }

    [Fact]
    public async Task SendAsync_DoesNothing_WhenNoCorrelationIdExistsOnHttpContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var response = await InvokeDelegatingHandlerAsync(accessor, request);

        Assert.False(response.RequestMessage!.Headers.Contains(CorrelationIdMiddlewareExtensions.HeaderName));
    }

    private static async Task<HttpResponseMessage> InvokeDelegatingHandlerAsync(IHttpContextAccessor accessor, HttpRequestMessage request)
    {
        var handler = new CorrelationIdDelegatingHandler(accessor)
        {
            InnerHandler = new StubHttpMessageHandler()
        };

        var method = typeof(CorrelationIdDelegatingHandler).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var task = (Task<HttpResponseMessage>)method!.Invoke(handler, [request, CancellationToken.None])!;
        return await task;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request
            });
        }
    }
}
