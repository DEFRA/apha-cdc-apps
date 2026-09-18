using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A species selected against a named disease filter, as returned by
/// <c>spgaSelectedDiseaseSpecies</c>. Carries the disease classification columns that the
/// plain species list does not.
/// </summary>
public sealed record SelectedSpecies : BaseEntity
{
    /// <summary>Gets the identifier of the parent species group.</summary>
    public required Guid ParentId { get; init; }

    /// <summary>Gets the display name. Sourced from the <c>Species.Name</c> column.</summary>
    public required string Description { get; init; }

    /// <summary>Gets a value indicating whether the species is currently effective.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether any profile version references this species.</summary>
    public required bool IsInUse { get; init; }

    /// <summary>Gets the disease name the filter was applied for.</summary>
    public required string DiseaseName { get; init; }

    /// <summary>Gets the endemic classification: 0 endemic, 1 other, 2 both.</summary>
    public required int Disease1 { get; init; }

    /// <summary>Gets the zoonotic classification: 0 zoonotic, 1 other, 2 both.</summary>
    public required int Disease2 { get; init; }

    /// <summary>Gets the notifiable classification: 0 notifiable, 1 other, 2 both.</summary>
    public required int Disease3 { get; init; }

    /// <summary>Gets the infectious classification: 0 infectious, 1 other, 2 both.</summary>
    public required int Disease4 { get; init; }

    /// <summary>Gets the free-text fifth disease filter value.</summary>
    public required string Disease5 { get; init; }

    /// <summary>Gets the ordering/grouping number applied by the disease filter.</summary>
    public required long FilterNumber { get; init; }
}
