namespace CDC.Api.Domain.Entities;

/// <summary>
/// Which review each profile role is responsible for. The role names come from <c>luProfileUserRole</c>; the review is
/// not a column there, so it is read from the name: technical roles own the author review, policy roles the policy
/// review and VA roles the VA review.
/// </summary>
public static class ProfileRoleCatalog
{
    private static readonly Dictionary<string, ReviewType> ReviewTypesByRoleName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Technical author"] = ReviewType.Author,
        ["Technical reviewer"] = ReviewType.Author,
        ["Tech author"] = ReviewType.Author,
        ["Tech reviewer"] = ReviewType.Author,
        ["Policy author"] = ReviewType.Policy,
        ["Policy reviewer"] = ReviewType.Policy,
        ["VA author"] = ReviewType.Va,
        ["VA reviewer"] = ReviewType.Va
    };

    /// <summary>Gets the review a role is responsible for.</summary>
    /// <param name="roleName">The role's name.</param>
    /// <returns>The review, or <see langword="null"/> when the name is not one of the six profile roles.</returns>
    public static ReviewType? ReviewTypeOf(string? roleName) =>
        roleName is not null && ReviewTypesByRoleName.TryGetValue(roleName.Trim(), out var reviewType) ? reviewType : null;
}
