using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages;

/// <summary>
/// Displays the fixed-section species data accordion for a single species. Replaces the legacy
/// <c>EditSpecies.aspx</c> page in its read-only ("View species data") mode.
/// </summary>
public class EditSpeciesModel : PageModel
{
    private readonly ISpeciesApiService speciesApiService;
    private readonly ILogger<EditSpeciesModel> logger;

    public EditSpeciesModel(ISpeciesApiService speciesApiService, ILogger<EditSpeciesModel> logger)
    {
        this.speciesApiService = speciesApiService;
        this.logger = logger;
    }

    /// <summary>Gets or sets the species being viewed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid SpeciesId { get; set; }

    /// <summary>Gets or sets the selected left-nav section key, bound from the querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Section { get; set; }

    /// <summary>Gets the species' current display name, once loaded.</summary>
    public string? SpeciesName { get; private set; }

    /// <summary>Gets a value indicating whether the species failed to load.</summary>
    public bool HasError { get; private set; }

    public EditSpeciesSection CurrentSection { get; private set; } = EditSpeciesSectionCatalog.Sections[0];

    public EditSpeciesSection? PreviousSection { get; private set; }

    public EditSpeciesSection? NextSection { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentSection = EditSpeciesSectionCatalog.GetByKeyOrDefault(Section);
        var sections = EditSpeciesSectionCatalog.Sections;
        var currentIndex = sections.ToList().IndexOf(CurrentSection);
        PreviousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
        NextSection = currentIndex < sections.Count - 1 ? sections[currentIndex + 1] : null;

        try
        {
            var species = await speciesApiService.GetSpeciesDetailAsync(SpeciesId, cancellationToken);

            if (species is null)
            {
                return NotFound();
            }

            SpeciesName = species.Name;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.SpeciesDetailLoadFailed(exception, SpeciesId);
            HasError = true;
        }

        return Page();
    }

    /// <summary>Builds the querystring URL for a left-nav/pagination link to a different section of this species.</summary>
    public string? BuildSectionUrl(string sectionKey) =>
        Url.Page("/EditSpecies", new { speciesId = SpeciesId, section = sectionKey });
}

/// <summary>Source-generated structured log messages for <see cref="EditSpeciesModel"/>.</summary>
internal static partial class EditSpeciesLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Error, Message = "Failed to load species '{SpeciesId}' detail from CDC.Api")]
    public static partial void SpeciesDetailLoadFailed(this ILogger logger, Exception exception, Guid speciesId);
}
