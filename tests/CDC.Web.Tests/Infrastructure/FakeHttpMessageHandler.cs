using System.Net;
using System.Text;

namespace CDC.Web.Tests.Infrastructure;

/// <summary>Returns a fixed status code and body for every request, regardless of URL.</summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
}
