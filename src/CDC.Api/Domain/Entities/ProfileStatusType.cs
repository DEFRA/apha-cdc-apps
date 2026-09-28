using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A profile status a profile can be set to. Mirrors the legacy <c>ProfileStatusType</c> data
/// contract returned by <c>spgaProfileStatusType</c>.
/// </summary>
public sealed record ProfileStatusType : BaseEntity
{
    /// <summary>Gets the status display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets a value indicating whether a profile in this status requires no further validation.</summary>
    public required bool IsValidationComplete { get; init; }
}
