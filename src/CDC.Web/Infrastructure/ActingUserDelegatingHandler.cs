using CDC.Common.Auth;
using CDC.Web.Features.Account;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Tells CDC.Api which signed-in user each call is made for, by forwarding their <c>[User].Id</c>
/// (the <see cref="ExternalUserClaimTypes.InternalUserId"/> claim) in the <see cref="ActingUserHeader"/>
/// header. CDC.Api records that user against the changes they make and reads their role flags from
/// the database, as the legacy <c>ProfilesIdentity</c> did. A request with no signed-in user, or a
/// limited-access user with no <c>[User]</c> row, is sent without the header and so reaches the API
/// as anonymous.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current request, if there is one.</param>
public sealed class ActingUserDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
