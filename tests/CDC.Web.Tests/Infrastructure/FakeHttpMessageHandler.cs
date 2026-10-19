using System.Net;
using System.Text;

namespace CDC.Web.Tests.Infrastructure;

/// <summary>Returns a fixed status code and body for every request, regardless of URL, and records the last request made.</summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    /// <summary>Gets the method of the most recent request.</summary>
    public HttpMethod? LastMethod { get; private set; }

    /// <summary>Gets the URI of the most recent request.</summary>
    public Uri? LastUri { get; private set; }

    /// <summary>Gets the body of the most recent request, or <see langword="null"/> when it had none.</summary>
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastUri = request.RequestUri;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}
