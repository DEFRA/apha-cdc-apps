namespace CDC.Web.Models;

public sealed record HeaderMenuLink(string Text, string PagePath);

public sealed record HeaderMenuSection(string Title, IReadOnlyList<HeaderMenuLink> Links);

/// Static definition of the header "Menu" navigation panel sections and links.
public static class HeaderMenu
{
    public static readonly IReadOnlyList<HeaderMenuSection> Sections = new List<HeaderMenuSection>
    {
        new("Disease Profiles", new List<HeaderMenuLink>
        {
            new("Search disease profiles", "/DiseaseProfiles/Search"),
            new("Create profile", "/DiseaseProfiles/Create"),
            new("Compare profile versions", "/DiseaseProfiles/CompareVersions"),
            new("Review Timings", "/DiseaseProfiles/ReviewTimings"),
        }),
        new("Reports", new List<HeaderMenuLink>
        {
            new("General reports", "/Reports/General"),
            new("Questions and guidance reports", "/Reports/QuestionsGuidance"),
            new("Disease ranking report", "/Reports/DiseaseRanking"),
        }),
        new("Species Data", new List<HeaderMenuLink>
        {
            new("View species data", "/ViewSpeciesData"),
            new("Maintain species data", "/SpeciesData/Maintain"),
        }),
    };
}
