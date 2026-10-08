using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization.Requirements;

/// <summary>
/// A single reusable requirement: the user must be in at least one of <see cref="AllowedRoles"/>.
/// Every navigation/page/report policy is built from this one requirement type, parameterized by
/// role list, rather than one bespoke requirement class per policy.
/// </summary>
/// <param name="allowedRoles">The roles permitted to satisfy this requirement. Must not be empty -
/// list every role the policy allows explicitly, rather than relying on an "all roles" special case.</param>
public sealed class AllowedRolesRequirement(params IReadOnlyCollection<string> allowedRoles) : IAuthorizationRequirement
{
    /// <summary>Gets the roles permitted to satisfy this requirement.</summary>
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles.Count > 0
        ? allowedRoles
        : throw new ArgumentException("At least one allowed role is required.", nameof(allowedRoles));
}
