namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// A species selected against a named disease filter.
/// </summary>
public sealed record SelectedSpeciesDto
{
    /// <summary>Gets the species identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the parent species group.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public bool IsInUse { get; init; }

    /// <summary>Gets the disease name the filter was applied for.</summary>
    public string DiseaseName { get; init; } = string.Empty;

    /// <summary>Gets the endemic classification: 0 endemic, 1 other, 2 both.</summary>
    public int Disease1 { get; init; }

    /// <summary>Gets the zoonotic classification: 0 zoonotic, 1 other, 2 both.</summary>
    public int Disease2 { get; init; }

    /// <summary>Gets the notifiable classification: 0 notifiable, 1 other, 2 both.</summary>
    public int Disease3 { get; init; }

    /// <summary>Gets the infectious classification: 0 infectious, 1 other, 2 both.</summary>
    public int Disease4 { get; init; }

    /// <summary>Gets the free-text fifth disease filter value.</summary>
    public string Disease5 { get; init; } = string.Empty;

    /// <summary>Gets the ordering/grouping number applied by the disease filter.</summary>
    public long FilterNumber { get; init; }
}
