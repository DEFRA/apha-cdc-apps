namespace CDC.Api.Features.Users.Dtos;

/// <summary>
/// Resolved internal user, returned to CDC.Web after an Entra ID sign-in. A user with no
/// <c>[dbo].[User]</c> row yet is created first, with both role flags <see langword="false"/>.
/// </summary>
public sealed record InternalUserDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets a value indicating whether the user can author/publish profiles.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    public required bool IsPolicyProfileUser { get; init; }
}
