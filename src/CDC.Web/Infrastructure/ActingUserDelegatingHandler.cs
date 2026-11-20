using CDC.Common.Auth;
using CDC.Web.Features.Account;
using Microsoft.Extensions.Options;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Authenticates every call to CDC.Api with the shared service key (<see cref="ServiceKeyHeader"/>) and tells it
/// which signed-in user the call is made for, by forwarding their <c>[User].Id</c>
/// (the <see cref="ExternalUserClaimTypes.InternalUserId"/> claim) in the <see cref="ActingUserHeader"/>
/// header. CDC.Api records that user against the changes they make and reads their role flags from
/// the database, as the legacy <c>ProfilesIdentity</c> did. A request with no signed-in user, or a
/// limited-access user with no <c>[User]</c> row, is sent without the acting-user header and so reaches the API
/// as anonymous.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current request, if there is one.</param>
/// <param name="apiOptions">CDC.Api settings, holding the service key.</param>
public sealed class ActingUserDelegatingHandler(IHttpContextAccessor httpContextAccessor, IOptions<ApiOptions> apiOptions) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Remove(ServiceKeyHeader.Name);
        request.Headers.Add(ServiceKeyHeader.Name, apiOptions.Value.ServiceKey);

        // Never let a caller-set header stand in for the signed-in user.
        request.Headers.Remove(ActingUserHeader.Name);

        var claim = httpContextAccessor.HttpContext?.User.FindFirst(ExternalUserClaimTypes.InternalUserId)?.Value;

        if (Guid.TryParse(claim, out var userId) && userId != Guid.Empty)
        {
            request.Headers.Add(ActingUserHeader.Name, userId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
