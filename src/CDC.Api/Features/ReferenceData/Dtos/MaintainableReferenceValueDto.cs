namespace CDC.Api.Features.ReferenceData.Dtos;

/// <summary>One value of a maintainable reference table, as <c>spgReferenceValueByTable</c> returns it.</summary>
public sealed record MaintainableReferenceValueDto
{
    /// <summary>Gets the reference value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the lookup text, which may carry <c>&lt;em&gt;</c> italics.</summary>
    public string LookupValue { get; init; } = string.Empty;

    /// <summary>Gets the date the value came into force, if recorded.</summary>
    public DateTime? EffectiveDateFrom { get; init; }

    /// <summary>Gets the date the value was inactivated, or <see langword="null"/> while it is active.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets a value indicating whether any profile answer references the value.</summary>
    public bool IsInUse { get; init; }

    /// <summary>Gets a value indicating whether the value is still in force.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the row version to send back when changing the value. Only returned when a single value is read;
    /// <see langword="null"/> in a table's value list.
    /// </summary>
    public byte[]? LastUpdated { get; init; }
}
