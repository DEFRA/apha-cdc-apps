using CDC.Api.Domain.Entities;

namespace CDC.Api.Application;

/// <summary>
/// The signed-in user making the request: their <c>[User].Id</c> and the profile-authoring role
/// flags of the legacy <c>ProfilesIdentity</c> business object (<c>IsProfileEditor</c>,
/// <c>IsUserManagementSystem</c>, <c>IsPolicyProfileUser</c>) used throughout <c>Profile.vb</c> and
/// <c>ProfileContributorList.vb</c> to gate actions.
/// </summary>
/// <remarks>
/// CDC.Web forwards the signed-in user's id and <c>ActingUserMiddleware</c> reads the flags from the
/// database, so they always reflect the user's current record. A request with no known user is
/// anonymous: it has no id and no flag set.
/// </remarks>
public interface IUserContext
{
    /// <summary>Gets the acting user's <c>[User].Id</c>, or <see langword="null"/> when the request carries no known user.</summary>
    Guid? UserId { get; }

    /// <summary>Gets a value indicating whether the user can author/publish profiles.</summary>
    bool IsProfileEditor { get; }

    /// <summary>Gets a value indicating whether the user is a read-only "user management system" account.</summary>
    bool IsUserManagementSystem { get; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    bool IsPolicyProfileUser { get; }
}

/// <summary>
/// The <see cref="IUserContext"/> for one request, filled in by <c>ActingUserMiddleware</c>. Until
/// it is, or when the request names no known user, the context is anonymous.
/// </summary>
public sealed class RequestUserContext : IUserContext
{
    private ActingUser? actingUser;

    /// <inheritdoc />
    public Guid? UserId => actingUser?.Id;

    /// <inheritdoc />
    public bool IsProfileEditor => actingUser?.IsProfileEditor ?? false;

    /// <summary>
    /// Gets a value that is always <see langword="false"/>: a user management system account never
    /// holds the editor or policy flag (the legacy <c>ProfilesIdentity</c> forbids the combination),
    /// so it is already refused everything those flags allow.
    /// </summary>
    public bool IsUserManagementSystem => false;

    /// <inheritdoc />
    public bool IsPolicyProfileUser => actingUser?.IsPolicyProfileUser ?? false;

    /// <summary>Makes <paramref name="user"/> the acting user for the rest of the request.</summary>
    /// <param name="user">The user CDC.Web forwarded, as read from the database.</param>
    internal void SetActingUser(ActingUser user) => actingUser = user;
}

/// <summary>Helpers for operations that must be attributed to a signed-in user.</summary>
public static class UserContextExtensions
{
    /// <summary>
    /// Returns the acting user's id for an operation that records who made the change - the audit
    /// columns are foreign keys to <c>[User]</c>, so an anonymous request cannot make it.
    /// </summary>
    /// <param name="userContext">The current request's user context.</param>
    /// <returns>The acting user's <c>[User].Id</c>.</returns>
    /// <exception cref="UnauthorizedAccessException">The request has no known user; the global exception handler turns this into a 403.</exception>
    public static Guid RequireUserId(this IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return userContext.UserId ?? throw new UnauthorizedAccessException("The request has no known signed-in user.");
    }
}
