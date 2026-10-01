namespace CDC.Web.Models;

/// <summary>One question shown in a species data section's accordion.</summary>
public sealed record EditSpeciesQuestion(string Number, string Text);

/// <summary>One of the fixed left-hand navigation sections on the "View species data" detail page.</summary>
public sealed record EditSpeciesSection(int Number, string Key, string Title, IReadOnlyList<EditSpeciesQuestion> Questions);

/// <summary>
/// The fixed section/question structure shown on the species data detail page, matching the
/// legacy <c>EditSpecies.aspx</c> layout. CDC.Api does not yet expose real per-species answer
/// content, so the question text below is sample/placeholder data standing in for that future API.
/// </summary>
public static class EditSpeciesSectionCatalog
{
    public static IReadOnlyList<EditSpeciesSection> Sections { get; } =
    [
        new EditSpeciesSection(1, "animal-identification", "Animal identification",
        [
            new EditSpeciesQuestion("1.1", "How are individual animals of this species identified (e.g. ear tags, passports, microchips)?"),
            new EditSpeciesQuestion("1.2", "What identification records are kept and for how long?")
        ]),
        new EditSpeciesSection(2, "movements", "Movements",
        [
            new EditSpeciesQuestion("2.1", "How easy is it to investigate the movement of a specific animal or group of animals (general requirements/anticipated modal approach by modal keeper of this species/group?)"),
            new EditSpeciesQuestion("2.2", "In addition to the above general requirements there may be specific records for certain types of holding situation")
        ]),
        new EditSpeciesSection(3, "animal-locations-and-number", "Animal locations and number",
        [
            new EditSpeciesQuestion("3.1", "How is the location of animals of this species recorded?"),
            new EditSpeciesQuestion("3.2", "How is the total number of animals of this species recorded?")
        ]),
        new EditSpeciesSection(4, "bio-security", "Bio-security",
        [
            new EditSpeciesQuestion("4.1", "What bio-security measures are typically in place for keepers of this species?")
        ]),
        new EditSpeciesSection(5, "surveillance", "Surveillance",
        [
            new EditSpeciesQuestion("5.1", "What surveillance arrangements exist for this species?")
        ]),
        new EditSpeciesSection(6, "international-trade", "International trade",
        [
            new EditSpeciesQuestion("6.1", "What international trade considerations apply to this species?")
        ])
    ];

    public static EditSpeciesSection GetByKeyOrDefault(string? key) =>
        Sections.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? Sections[0];
}
