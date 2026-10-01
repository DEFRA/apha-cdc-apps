namespace CDC.Web.Models;

/// <summary>One of the fixed left-hand navigation sections on the "View species data" detail page.</summary>
public sealed record EditSpeciesSection(int Number, string Key, string Title);

/// <summary>
/// The fixed, hardcoded left-nav section labels shown on the species data detail page, matching
/// the legacy <c>EditSpecies.aspx</c> layout. Each label is matched (by name) against a real
/// section returned by <c>GET /api/species/metadata</c> to load that section's actual questions.
/// </summary>
public static class EditSpeciesSectionCatalog
{
    public static IReadOnlyList<EditSpeciesSection> Sections { get; } =
    [
        new EditSpeciesSection(1, "animal-identification", "Animal identification"),
        new EditSpeciesSection(2, "movements", "Movements"),
        new EditSpeciesSection(3, "animal-locations-and-number", "Animal locations and number"),
        new EditSpeciesSection(4, "bio-security", "Bio-security"),
        new EditSpeciesSection(5, "surveillance", "Surveillance"),
        new EditSpeciesSection(6, "international-trade", "International trade")
    ];

    public static EditSpeciesSection GetByKeyOrDefault(string? key) =>
        Sections.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? Sections[0];
}
